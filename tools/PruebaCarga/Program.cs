using System.Diagnostics;
using System.Globalization;
using System.Text;
using PruebaCarga;

// Prueba de carga de Altares / Catrinas (F4, docs/01-decision-arquitectura.md §11).
//   dotnet run -c Release --project tools/PruebaCarga -- --url https://... --app altares --escenario mixto
// Credenciales SOLO por variables de entorno (nunca en argumentos ni en archivos versionados):
//   PRUEBA_ADMIN_USUARIO / PRUEBA_ADMIN_PASSWORD  cuenta admin de la base de STAGING
//   PRUEBA_CARGA_PASSWORD                         contraseña (>= 8) de las cuentas ficticias carga.*
// Escenarios: preparar | login | mixto | pdf | abuso. Ver tools/PruebaCarga/README.md.

var op = Opciones.Leer(args);
Cliente.AceptarCertificadoLocal = args.Contains("--certificado-local");
IAppBajoPrueba app = op.App == "catrinas" ? new AppCatrinas() : new AppAltares();
var metricas = new Metricas();
var adminUsuario = Variable("PRUEBA_ADMIN_USUARIO");
var adminPassword = Variable("PRUEBA_ADMIN_PASSWORD");
var password = Variable("PRUEBA_CARGA_PASSWORD");
if (password.Length < 8) Salir("PRUEBA_CARGA_PASSWORD debe tener al menos 8 caracteres.");

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {app.Nombre} {op.Url} escenario={op.Escenario}");
var reloj = Stopwatch.StartNew();
MonitorMemoria? monitor = null;
var notas = new List<string>();

switch (op.Escenario)
{
    case "preparar": await PrepararAsync(); break;
    case "login": await LoginAsync(); break;
    case "mixto": await MixtoAsync(); break;
    case "pdf": await PdfAsync(); break;
    case "abuso": await AbusoAsync(); break;
    default: Salir("Escenario desconocido."); break;
}

Reportar();
return;

async Task<Cliente> AdminAsync(string prefijo = "admin")
{
    var c = new Cliente(op.Url, metricas, prefijo);
    if (!await app.LoginJuezAsync(c, adminUsuario, adminPassword, "login admin"))
        Salir("No se pudo entrar como admin (¿contraseña correcta y ya cambiada la temporal?).");
    return c;
}

async Task PrepararAsync()
{
    var admin = await AdminAsync();
    var hechos = 0;
    await Parallel.ForEachAsync(Enumerable.Range(1, op.Equipos), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (i, _) =>
    {
        using var org = new Cliente(op.Url, metricas, "prep");
        using var integrante = new Cliente(op.Url, metricas, "prep");
        using var maestro = new Cliente(op.Url, metricas, "prep");
        await app.PrepararEquipoAsync(org, integrante, maestro, i, password);
        if (Interlocked.Increment(ref hechos) % 10 == 0) Console.WriteLine($"  equipos listos: {hechos}/{op.Equipos}");
    });

    for (var j = 1; j <= op.Jueces; j++)
    {
        await app.CrearJuezAsync(admin, Cuentas.Juez(j), password);
        using var juez = new Cliente(op.Url, metricas, "prep");
        if (!await app.LoginJuezAsync(juez, Cuentas.Juez(j), password)) Salir($"El juez {Cuentas.Juez(j)} no pudo entrar.");
    }

    // Equipos reservados para el escenario pdf: se cierran como Final aqui (el escenario mixto no los toca).
    using var juez1 = new Cliente(op.Url, metricas, "prep");
    await app.LoginJuezAsync(juez1, Cuentas.Juez(1), password);
    foreach (var id in await app.ObjetivosJuezAsync(juez1, Enumerable.Range(1, op.ReservadosPdf).ToList()))
    {
        await app.GuardarAsync(juez1, id, final: true);
    }
    notas.Add($"Preparados {op.Equipos} equipos ({(app is AppAltares ? 3 : 2)} cuentas c/u), {op.Jueces} jueces, {op.ReservadosPdf} evaluaciones Final para pdf.");
}

// Rafaga de logins distintos desde UNA IP (la red de la FIM detras de NAT), repartidos en --duracion segundos.
async Task LoginAsync()
{
    var cuentas = CuentasParticipantes(1).Take(op.Usuarios).ToList();
    if (cuentas.Count < op.Usuarios) notas.Add($"Solo hay {cuentas.Count} cuentas ficticias; se usaron todas.");
    var fallidos = 0;
    var tareas = cuentas.Select(async (cuenta, n) =>
    {
        await Task.Delay(TimeSpan.FromSeconds(op.Duracion * (double)n / cuentas.Count));
        using var c = new Cliente(op.Url, metricas, "rafaga");
        if (!await app.LoginParticipanteAsync(c, cuenta, password)) Interlocked.Increment(ref fallidos);
    });
    await Task.WhenAll(tareas);
    notas.Add($"{cuentas.Count} logins distintos en {op.Duracion}s desde una IP; logins no exitosos: {fallidos}.");
}

async Task MixtoAsync()
{
    using var fin = new CancellationTokenSource(TimeSpan.FromSeconds(op.Duracion));
    var monitorAdmin = await AdminAsync("monitor");
    monitor = new MonitorMemoria(monitorAdmin, app.RutaDiagnostico);
    var muestreo = monitor.MuestrearAsync(fin.Token);

    var administradores = 2;
    var participantes = CuentasParticipantes(op.ReservadosPdf + 1).Take(Math.Max(0, op.Usuarios - op.Jueces - administradores)).ToList();
    var equiposMixto = Enumerable.Range(op.ReservadosPdf + 1, op.Equipos - op.ReservadosPdf).ToList();
    var sinLogin = 0;
    var tareas = new List<Task>();

    foreach (var cuenta in participantes)
    {
        tareas.Add(Task.Run(async () =>
        {
            await Util.PausaAsync(0, op.Rampa * 1000, fin.Token);
            using var c = new Cliente(op.Url, metricas, "participante");
            if (!await app.LoginParticipanteAsync(c, cuenta, password)) { Interlocked.Increment(ref sinLogin); return; }
            for (var it = 0; !fin.IsCancellationRequested; it++)
            {
                await app.CicloParticipanteAsync(c, cuenta, it);
                await Util.PausaAsync(3000, 8000, fin.Token);
            }
        }));
    }

    for (var j = 1; j <= op.Jueces; j++)
    {
        var juezN = j;
        var asignados = equiposMixto.Where((_, k) => k % op.Jueces == juezN - 1).ToList();
        tareas.Add(Task.Run(async () =>
        {
            await Util.PausaAsync(0, op.Rampa * 1000, fin.Token);
            using var c = new Cliente(op.Url, metricas, "juez");
            if (!await app.LoginJuezAsync(c, Cuentas.Juez(juezN), password)) { Interlocked.Increment(ref sinLogin); return; }
            var objetivos = await app.ObjetivosJuezAsync(c, asignados);
            var cerrados = new HashSet<int>();
            for (var it = 0; !fin.IsCancellationRequested && objetivos.Count > 0; it++)
            {
                var objetivo = objetivos[it % objetivos.Count];
                // En el ultimo cuarto de la prueba cada juez cierra como Final sus equipos (una vez cada uno).
                var final = reloj.Elapsed.TotalSeconds > op.Duracion * 0.75 && cerrados.Add(objetivo);
                if (!final && cerrados.Contains(objetivo)) { await Util.PausaAsync(2000, 4000, fin.Token); continue; }
                await app.GuardarAsync(c, objetivo, final);
                await Util.PausaAsync(5000, 15000, fin.Token);
            }
        }));
    }

    for (var a = 0; a < administradores; a++)
    {
        tareas.Add(Task.Run(async () =>
        {
            using var c = await AdminAsync();
            while (!fin.IsCancellationRequested)
            {
                await app.CicloAdminAsync(c);
                await Util.PausaAsync(5000, 10000, fin.Token);
            }
        }));
    }

    await Task.WhenAll(tareas);
    await muestreo;
    notas.Add($"Usuarios simultaneos: {participantes.Count} participantes + {op.Jueces} jueces + {administradores} admins = {participantes.Count + op.Jueces + administradores}; sin login: {sinLogin}.");
}

async Task PdfAsync()
{
    using var fin = new CancellationTokenSource(TimeSpan.FromSeconds(op.Duracion));
    var admin = await AdminAsync("monitor");
    monitor = new MonitorMemoria(admin, app.RutaDiagnostico);
    var muestreo = monitor.MuestrearAsync(fin.Token);
    var evaluaciones = await app.EvaluacionesFinalesAsync(admin);
    if (evaluaciones.Count == 0) Salir("No hay evaluaciones para descargar (corre 'preparar' primero).");

    var tareas = Enumerable.Range(0, op.ConcurrenciaPdf).Select(_ => Task.Run(async () =>
    {
        using var c = await AdminAsync("pdf");
        while (!fin.IsCancellationRequested)
        {
            await app.DescargasAsync(c, evaluaciones[Util.Azar.Next(evaluaciones.Count)]);
            await Util.PausaAsync(1000, 3000, fin.Token);
        }
    })).ToList();
    await Task.WhenAll(tareas);
    await muestreo;
    notas.Add($"{op.ConcurrenciaPdf} descargas concurrentes sobre {evaluaciones.Count} evaluaciones.");
}

// Demuestra los tres casos del limitador sin tocar su configuracion.
async Task AbusoAsync()
{
    var victima = Cuentas.Organizador(op.Equipos);
    var vecino = Cuentas.Organizador(op.Equipos - 1);

    using (var atacante = new Cliente(op.Url, metricas, "abuso cuenta"))
    {
        var codigos = new List<int>();
        for (var k = 0; k < 8; k++)
        {
            await app.LoginParticipanteAsync(atacante, victima, "contraseña-incorrecta", "login con contraseña incorrecta");
            codigos.Add(metricas.Resumen().First(f => f.Operacion == "abuso cuenta login con contraseña incorrecta").Limitadas429);
        }
        var bloqueadaConCorrecta = !await app.LoginParticipanteAsync(atacante, victima, password, "login correcto de cuenta bloqueada");
        notas.Add($"Cuenta atacada: 429 acumulados tras cada intento = [{string.Join(",", codigos)}]; con la contraseña correcta sigue bloqueada: {bloqueadaConCorrecta}.");
    }

    using (var vecinoNat = new Cliente(op.Url, metricas, "abuso vecino"))
    {
        notas.Add($"Otra cuenta desde la MISMA IP entra normal: {await app.LoginParticipanteAsync(vecinoNat, vecino, password, "login vecino NAT")}.");
    }

    // Techo por IP: cuentas inexistentes distintas (el limite por cuenta no las frena), tan rapido como se pueda.
    var enviados = 0;
    var sw = Stopwatch.StartNew();
    await Parallel.ForEachAsync(Enumerable.Range(0, op.Usuarios), new ParallelOptions { MaxDegreeOfParallelism = 20 }, async (k, _) =>
    {
        using var c = new Cliente(op.Url, metricas, "abuso techo IP");
        await app.LoginParticipanteAsync(c, new Cuenta($"no.existe.{k}.{Guid.NewGuid():N}@uabc.edu.mx", "", "", 0, false), "x-incorrecta", "login masivo");
        Interlocked.Increment(ref enviados);
    });
    notas.Add($"Techo por IP: {enviados} intentos en {sw.Elapsed.TotalSeconds:F0}s; ver fila 'abuso techo IP login masivo' (429 esperados por encima del techo configurado).");
}

IEnumerable<Cuenta> CuentasParticipantes(int desdeEquipo)
{
    for (var i = desdeEquipo; i <= op.Equipos; i++)
    {
        yield return Cuentas.Organizador(i);
        yield return Cuentas.Integrante(i);
        if (app is AppAltares) yield return Cuentas.Maestro(i);
    }
}

void Reportar()
{
    var sb = new StringBuilder();
    sb.AppendLine($"# Prueba de carga {app.Nombre} — {op.Escenario}");
    sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm}; destino: {op.Url}; duracion real: {reloj.Elapsed.TotalSeconds:F0}s");
    sb.AppendLine();
    sb.AppendLine("| Operacion | Total | 5xx | Timeout | 429 | Otros 4xx | p50 ms | p95 ms | p99 ms | Max ms |");
    sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
    var filas = metricas.Resumen().ToList();
    foreach (var f in filas)
    {
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"| {f.Operacion} | {f.Total} | {f.Errores5xx} | {f.Timeouts} | {f.Limitadas429} | {f.Otros4xx} | {f.P50:F0} | {f.P95:F0} | {f.P99:F0} | {f.Max:F0} |"));
    }
    var total = filas.Sum(f => f.Total);
    var errores = filas.Sum(f => f.Errores5xx + f.Timeouts);
    sb.AppendLine();
    sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
        $"Peticiones medidas: {total}; errores (5xx + timeouts): {errores} ({(total == 0 ? 0 : 100.0 * errores / total):F2} %); 429: {filas.Sum(f => f.Limitadas429)}; throughput: {total / Math.Max(1, reloj.Elapsed.TotalSeconds):F1} req/s"));
    if (monitor != null)
    {
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Memoria del proceso (max muestreado cada 3 s): working set {monitor.MaxWorkingSetMB:F0} MB, privada {monitor.MaxPrivadaMB:F0} MB; limite visible para el GC {monitor.LimiteGcMB:F0} MB."));
        if (monitor.ReinicioDetectado != null) sb.AppendLine("ATENCION: " + monitor.ReinicioDetectado);
    }
    foreach (var n in notas) sb.AppendLine("- " + n);

    Console.WriteLine();
    Console.WriteLine(sb.ToString());
    if (op.Salida != null)
    {
        File.WriteAllText(op.Salida, sb.ToString());
        Console.WriteLine($"Resultado guardado en {op.Salida}");
    }
}

static string Variable(string nombre) =>
    Environment.GetEnvironmentVariable(nombre) is { Length: > 0 } v ? v : throw new InvalidOperationException($"Falta la variable de entorno {nombre}.");

static void Salir(string mensaje)
{
    Console.Error.WriteLine("ERROR: " + mensaje);
    Environment.Exit(2);
}

sealed record Opciones(string Url, string App, string Escenario, int Equipos, int Jueces, int ReservadosPdf, int Usuarios,
    int Duracion, int Rampa, int ConcurrenciaPdf, string? Salida)
{
    public static Opciones Leer(string[] args)
    {
        string? Arg(string nombre) { var i = Array.IndexOf(args, "--" + nombre); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        int Num(string nombre, int defecto) => int.TryParse(Arg(nombre), out var n) ? n : defecto;
        var url = Arg("url") ?? throw new ArgumentException("Falta --url");
        return new Opciones(url.TrimEnd('/'), Arg("app") ?? "altares", Arg("escenario") ?? "mixto",
            Num("equipos", 70), Num("jueces", 20), Num("reservados-pdf", 10), Num("usuarios", 200),
            Num("duracion", 300), Num("rampa", 30), Num("pdf-concurrencia", 20), Arg("salida"));
    }
}
