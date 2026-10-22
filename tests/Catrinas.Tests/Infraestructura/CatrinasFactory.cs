using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Catrinas.Web.Datos;
using Catrinas.Web.Modelos;
using Concurso.Core.Seguridad;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Catrinas.Tests.Infraestructura;

// Levanta Catrinas.Web real (entorno Production) contra una LocalDB desechable y VACIA: el arranque
// aplica unicamente las migraciones de Catrinas. No existe ninguna base ni proceso de Altares.
public class CatrinasFactory : WebApplicationFactory<global::Program>
{
    public const string Host = "https://catrinas.test";
    public const string AdminUsuario = "admin-catrinas-pruebas";
    public const string AdminPassword = "Temporal-Catrinas-2026!";
    public const string CabeceraIpRemota = "X-Test-Remote-IP";

    public string NombreBase { get; } = $"CatrinasTests_{Guid.NewGuid():N}";
    public string CadenaConexion => $"Server=(localdb)\\MSSQLLocalDB;Database={NombreBase};Trusted_Connection=True;MultipleActiveResultSets=true";
    public ConcurrentQueue<string> Logs { get; } = new();

    public CatrinasFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__CatrinasContext", CadenaConexion);
        Environment.SetEnvironmentVariable("GoogleAuth__ClientId", "cliente-catrinas-pruebas.apps.googleusercontent.com");
        Environment.SetEnvironmentVariable("GoogleAuth__ClientSecret", "secreto-catrinas-pruebas");
        Environment.SetEnvironmentVariable("SEED_ADMIN_USUARIO", AdminUsuario);
        Environment.SetEnvironmentVariable("SEED_ADMIN_PASSWORD", AdminPassword);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(s => s.AddSingleton<IStartupFilter, IpRemotaDePrueba>());
        builder.ConfigureLogging(l => l.AddProvider(new LogsCapturados(Logs)));
    }

    public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri(Host),
        HandleCookies = true
    });

    public T ConContexto<T>(Func<CatrinasDbContext, T> accion)
    {
        using var scope = Services.CreateScope();
        return accion(scope.ServiceProvider.GetRequiredService<CatrinasDbContext>());
    }

    public async Task ConContextoAsync(Func<CatrinasDbContext, Task> accion)
    {
        using var scope = Services.CreateScope();
        await accion(scope.ServiceProvider.GetRequiredService<CatrinasDbContext>());
    }

    public async Task<int> CrearJuezAsync(string usuario, string password, bool admin = false)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatrinasDbContext>();
        var juez = new Juez
        {
            Usuario = usuario,
            NombreCompleto = "Jurado " + usuario,
            Password = PasswordHashService.HashPassword(password),
            Rol = admin ? RolesJuez.Admin : RolesJuez.Juez
        };
        context.Jueces.Add(juez);
        await context.SaveChangesAsync();
        return juez.Id;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var scope = Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<CatrinasDbContext>().Database.EnsureDeleted();
            }
            catch
            {
                // La limpieza no debe ocultar el resultado de las pruebas.
            }
        }
        base.Dispose(disposing);
    }
}

[CollectionDefinition(Nombre)]
public class CatrinasCollection : ICollectionFixture<CatrinasFactory>
{
    public const string Nombre = "Catrinas";
}

public class IpRemotaDePrueba : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, siguiente) =>
        {
            var ip = context.Request.Headers[CatrinasFactory.CabeceraIpRemota].FirstOrDefault();
            context.Connection.RemoteIpAddress = IPAddress.Parse(string.IsNullOrEmpty(ip) ? "203.0.113.200" : ip);
            await siguiente();
        });
        next(app);
    };
}

public sealed class LogsCapturados : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _destino;
    public LogsCapturados(ConcurrentQueue<string> destino) => _destino = destino;
    public ILogger CreateLogger(string categoryName) => new Capturador(_destino);
    public void Dispose() { }

    private sealed class Capturador : ILogger
    {
        private readonly ConcurrentQueue<string> _destino;
        public Capturador(ConcurrentQueue<string> destino) => _destino = destino;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _destino.Enqueue(formatter(state, exception));
            if (exception != null) _destino.Enqueue("Exception: " + exception);
            if (state is IEnumerable<KeyValuePair<string, object?>> valores)
            {
                foreach (var v in valores) _destino.Enqueue($"{v.Key}={v.Value}");
            }
        }
    }
}

// Acciones HTTP reutilizables para los flujos de Catrinas.
public static class Http
{
    public static async Task<string> TokenAsync(HttpClient cliente, string ruta = "/Registro/Login")
    {
        var html = await cliente.GetStringAsync(ruta);
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(m.Success, $"Sin token antiforgery en {ruta}");
        return m.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string accion, params (string, string)[] campos) =>
        await PostDesdeAsync(cliente, "/Registro/Login", accion, campos);

    public static async Task<HttpResponseMessage> PostDesdeAsync(HttpClient cliente, string rutaFormulario, string accion, params (string, string)[] campos)
    {
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = await TokenAsync(cliente, rutaFormulario);
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }

    public static Task<HttpResponseMessage> SignupAsync(HttpClient cliente, string nombre, string identificador, string correo,
        string password = "Participante-2026!", params (string, string)[] extra) =>
        PostAsync(cliente, "/Registro/Signup",
            new[] { ("NombreCompleto", nombre), ("Identificador", identificador), ("CorreoInstitucional", correo),
                    ("Password", password), ("ConfirmarPassword", password) }.Concat(extra).ToArray());

    public static Task<HttpResponseMessage> LoginJuradoAsync(HttpClient cliente, string usuario, string password) =>
        PostAsync(cliente, "/Acceso/Login", ("usuario", usuario), ("password", password));

    public static Task<HttpResponseMessage> CrearInscripcionAsync(HttpClient cliente, string nombre, int carreraId, params (string, string)[] extra) =>
        PostAsync(cliente, "/Inscripcion/Crear", new[] { ("Nombre", nombre), ("CarreraId", carreraId.ToString()) }.Concat(extra).ToArray());

    // M4: los avisos de la pantalla de inicio viajan en TempData (ya no en ?error=). Comprueba que la respuesta
    // redirige a esa pantalla (la ruta por defecto) y devuelve su texto, con el mismo cliente para que viaje la cookie.
    public static async Task<string> AvisoDeLoginAsync(HttpClient cliente, HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var destino = r.Headers.Location!.OriginalString;
        Assert.Matches("^/(\\?tab=\\w+)?$", destino);
        return WebUtility.HtmlDecode(await cliente.GetStringAsync(destino));
    }

    public static void Redirige(HttpResponseMessage r, string contiene)
    {
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains(contiene, r.Headers.Location!.OriginalString);
    }

    public static async Task<HttpResponseMessage> GuardarEvaluacionAsync(HttpClient cliente, int inscripcionId, string accion,
        decimal v, decimal m, decimal a, decimal o, decimal t, bool confirmar = true, string? rowVersion = null)
    {
        var ruta = $"/Evaluacion/Evaluar?inscripcionId={inscripcionId}";
        if (rowVersion == null)
        {
            var html = await cliente.GetStringAsync(ruta);
            rowVersion = Regex.Match(html, "name=\"rowVersion\" value=\"([^\"]*)\"").Groups[1].Value;
        }
        string D(decimal x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return await PostDesdeAsync(cliente, ruta, "/Evaluacion/Guardar",
            ("inscripcionId", inscripcionId.ToString()), ("vestuario", D(v)), ("maquillaje", D(m)), ("accesorios", D(a)),
            ("originalidadCreatividad", D(o)), ("apegoTradiciones", D(t)), ("accion", accion),
            ("confirmacion", confirmar ? "CONFIRMO" : ""), ("rowVersion", System.Net.WebUtility.HtmlDecode(rowVersion)));
    }
}
