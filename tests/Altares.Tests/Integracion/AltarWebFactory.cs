using System.Collections.Concurrent;
using System.Net;
using AltarWeb.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Altares.Tests.Integracion;

// Levanta la aplicacion real (Program.cs completo, entorno Production) contra una base LocalDB
// desechable: SeedData aplica todas las migraciones y crea el admin semilla. La base se borra al
// terminar. Requiere SQL Server LocalDB (Windows), por eso estas pruebas llevan la categoria
// "Integracion" y pueden excluirse con: dotnet test --filter Category!=Integracion
public class AltarWebFactory : WebApplicationFactory<Program>
{
    // HSTS excluye "localhost" por defecto; un host .test permite verificar la cabecera.
    public const string Host = "https://altarweb.test";
    public const string AdminUsuario = "admin-pruebas";
    public const string AdminPassword = "Temporal-Pruebas-2026!";

    private readonly string _connectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database=AltarWebTests_{Guid.NewGuid():N};Trusted_Connection=True;MultipleActiveResultSets=true";

    public AltarWebFactory()
    {
        // Variables de entorno: es la fuente de configuracion que Program.cs ve con seguridad antes
        // de construir el host. Sustituyen al appsettings.json local (no versionado).
        Environment.SetEnvironmentVariable("ConnectionStrings__AltarContext", _connectionString);
        Environment.SetEnvironmentVariable("GoogleAuth__ClientId", "cliente-de-prueba.apps.googleusercontent.com");
        Environment.SetEnvironmentVariable("GoogleAuth__ClientSecret", "secreto-de-prueba");
        Environment.SetEnvironmentVariable("SEED_ADMIN_USUARIO", AdminUsuario);
        Environment.SetEnvironmentVariable("SEED_ADMIN_PASSWORD", AdminPassword);
        // SMTP hacia un puerto local cerrado: ninguna prueba envia correo real y los errores SMTP se pueden probar.
        Environment.SetEnvironmentVariable("Smtp__Host", "127.0.0.1");
        Environment.SetEnvironmentVariable("Smtp__Port", PuertoSmtpCerrado.ToString());
        Environment.SetEnvironmentVariable("Smtp__EnableSsl", "false");
        Environment.SetEnvironmentVariable("Smtp__User", "altares-pruebas@ejemplo.test");
        Environment.SetEnvironmentVariable("Smtp__Password", "no-real");
        Environment.SetEnvironmentVariable("Smtp__TimeoutSegundos", "5");
    }

    // Puerto que se abre y se cierra: nadie escucha ahi durante las pruebas.
    private static readonly int PuertoSmtpCerrado = ObtenerPuertoCerrado();
    private static int ObtenerPuertoCerrado()
    {
        var escucha = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((System.Net.IPEndPoint)escucha.LocalEndpoint).Port;
        escucha.Stop();
        return puerto;
    }

    // IP del cliente que ve la app cuando la prueba no indica otra (TEST-NET-3, RFC 5737).
    public const string IpPorDefecto = "203.0.113.250";

    // Cabecera SOLO de pruebas: simula la IP de la conexion TCP (lo que en produccion fija el servidor y
    // no puede falsificar un cliente). X-Forwarded-For, en cambio, si lo envia cualquiera.
    public const string CabeceraIpRemota = "X-Test-Remote-IP";

    // Todo lo que la app escribe al log durante las pruebas, para verificar que no se filtran secretos.
    public ConcurrentQueue<string> Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, IpRemotaDePruebaStartupFilter>());
        builder.ConfigureLogging(logging => logging.AddProvider(new ProveedorLogsCapturados(Logs)));
    }

    // Crea un juez activo con contraseña conocida directamente en la base de pruebas.
    public async Task CrearJuezAsync(string usuario, string password, string? correo = null, string rol = "Juez")
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
        context.Jueces.Add(new Juez
        {
            Usuario = usuario,
            CorreoInstitucional = correo,
            Password = Concurso.Core.Seguridad.PasswordHashService.HashPassword(password),
            Rol = rol,
            NombreCompleto = usuario
        });
        await context.SaveChangesAsync();
    }

    public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri(Host),
        HandleCookies = true
    });

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var scope = Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<AltarDbContext>().Database.EnsureDeleted();
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
public class AppCollection : ICollectionFixture<AltarWebFactory>
{
    public const string Nombre = "App";
}

// Corre antes de todo el pipeline de la app (incluido ForwardedHeaders), igual que el servidor real.
public class IpRemotaDePruebaStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, siguiente) =>
        {
            var ip = context.Request.Headers[AltarWebFactory.CabeceraIpRemota].FirstOrDefault();
            context.Connection.RemoteIpAddress = IPAddress.Parse(string.IsNullOrEmpty(ip) ? AltarWebFactory.IpPorDefecto : ip);
            await siguiente();
        });
        next(app);
    };
}

public sealed class ProveedorLogsCapturados : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _destino;

    public ProveedorLogsCapturados(ConcurrentQueue<string> destino) => _destino = destino;

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
            if (state is IEnumerable<KeyValuePair<string, object?>> valores)
            {
                foreach (var valor in valores) _destino.Enqueue($"{valor.Key}={valor.Value}");
            }
        }
    }
}
