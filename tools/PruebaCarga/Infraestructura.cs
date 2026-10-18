using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PruebaCarga;

// Metricas por operacion: latencias, codigos, timeouts y 429.
public sealed class Metricas
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<(double Ms, int Codigo)>> _datos = new();

    public void Registrar(string operacion, double ms, int codigo) => _datos.GetOrAdd(operacion, _ => new()).Add((ms, codigo));

    public IEnumerable<FilaMetrica> Resumen() => _datos.OrderBy(d => d.Key).Select(d =>
    {
        var lista = d.Value.ToList();
        var tiempos = lista.Select(x => x.Ms).OrderBy(x => x).ToArray();
        double P(double q) => tiempos.Length == 0 ? 0 : tiempos[Math.Min(tiempos.Length - 1, (int)Math.Ceiling(q * tiempos.Length) - 1)];
        return new FilaMetrica(d.Key, lista.Count,
            lista.Count(x => x.Codigo >= 500), lista.Count(x => x.Codigo == 0), lista.Count(x => x.Codigo == 429),
            lista.Count(x => x.Codigo >= 400 && x.Codigo < 500 && x.Codigo != 429), P(0.50), P(0.95), P(0.99), tiempos.LastOrDefault());
    });
}

public sealed record FilaMetrica(string Operacion, int Total, int Errores5xx, int Timeouts, int Limitadas429, int Otros4xx,
    double P50, double P95, double P99, double Max);

// Un navegador virtual: cookies propias, sin seguir redirecciones, timeout de 30 s.
public sealed class Cliente : IDisposable
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
    private readonly HttpClient _http;
    private readonly Metricas _m;
    public string Prefijo { get; }

    // Solo para la linea base local (certificado de desarrollo no confiable). Nunca aplica fuera de localhost.
    public static bool AceptarCertificadoLocal { get; set; }

    public Cliente(string baseUrl, Metricas m, string prefijo)
    {
        _http = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions = AceptarCertificadoLocal && new Uri(baseUrl).IsLoopback
                ? new System.Net.Security.SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true }
                : new System.Net.Security.SslClientAuthenticationOptions()
        })
        { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
        _m = m;
        Prefijo = prefijo;
    }

    public async Task<(int Codigo, string Cuerpo, string? Destino)> GetAsync(string ruta, string operacion, bool leerCuerpo = true)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var r = await _http.GetAsync(ruta);
            var cuerpo = leerCuerpo ? await r.Content.ReadAsStringAsync() : (await r.Content.ReadAsByteArrayAsync()).Length.ToString(CultureInfo.InvariantCulture);
            _m.Registrar($"{Prefijo} {operacion}", sw.Elapsed.TotalMilliseconds, (int)r.StatusCode);
            return ((int)r.StatusCode, cuerpo, r.Headers.Location?.OriginalString);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            _m.Registrar($"{Prefijo} {operacion}", sw.Elapsed.TotalMilliseconds, 0);
            return (0, string.Empty, null);
        }
    }

    // Lee el token antiforgery de rutaFormulario (sin medirlo) y hace el POST medido.
    public async Task<(int Codigo, string Cuerpo, string? Destino)> PostAsync(string rutaFormulario, string accion, string operacion,
        params (string, string)[] campos)
    {
        string html;
        try { html = await _http.GetStringAsync(rutaFormulario); }
        catch { _m.Registrar($"{Prefijo} {operacion}", 0, 0); return (0, string.Empty, null); }

        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value;
        var sw = Stopwatch.StartNew();
        try
        {
            using var r = await _http.PostAsync(accion, new FormUrlEncodedContent(datos));
            var cuerpo = await r.Content.ReadAsStringAsync();
            _m.Registrar($"{Prefijo} {operacion}", sw.Elapsed.TotalMilliseconds, (int)r.StatusCode);
            return ((int)r.StatusCode, cuerpo, r.Headers.Location?.OriginalString);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            _m.Registrar($"{Prefijo} {operacion}", sw.Elapsed.TotalMilliseconds, 0);
            return (0, string.Empty, null);
        }
    }

    public Task<string> HtmlAsync(string ruta) => _http.GetStringAsync(ruta);

    public void Dispose() => _http.Dispose();
}

// Lee /…/Diagnostico con una sesion de admin cada pocos segundos y guarda el maximo de memoria.
public sealed class MonitorMemoria
{
    private readonly Cliente _admin;
    private readonly string _ruta;
    public double MaxWorkingSetMB { get; private set; }
    public double MaxPrivadaMB { get; private set; }
    public double LimiteGcMB { get; private set; }
    public string? ReinicioDetectado { get; private set; }
    private string? _inicio;

    public MonitorMemoria(Cliente admin, string ruta) { _admin = admin; _ruta = ruta; }

    public async Task MuestrearAsync(CancellationToken fin)
    {
        while (!fin.IsCancellationRequested)
        {
            try
            {
                using var doc = JsonDocument.Parse(await _admin.HtmlAsync(_ruta));
                var raiz = doc.RootElement;
                MaxWorkingSetMB = Math.Max(MaxWorkingSetMB, raiz.GetProperty("memoria.workingSetMB").GetDouble());
                MaxPrivadaMB = Math.Max(MaxPrivadaMB, raiz.GetProperty("memoria.privadaMB").GetDouble());
                LimiteGcMB = raiz.GetProperty("memoria.gcLimiteDisponibleMB").GetDouble();
                var inicio = raiz.GetProperty("proceso.inicioUtc").GetString();
                if (_inicio != null && inicio != _inicio) ReinicioDetectado = $"el proceso se reinicio ({_inicio} -> {inicio})";
                _inicio = inicio;
            }
            catch
            {
                // Una muestra fallida no detiene la prueba (puede ser justo un 5xx bajo carga).
            }
            try { await Task.Delay(3000, fin); } catch (TaskCanceledException) { }
        }
    }
}

public static class Util
{
    public static readonly Random Azar = new();
    public static Task PausaAsync(int minMs, int maxMs, CancellationToken ct) =>
        Task.Delay(Azar.Next(minMs, maxMs), ct).ContinueWith(_ => { });
    public static string D(decimal x) => x.ToString(CultureInfo.InvariantCulture);
}
