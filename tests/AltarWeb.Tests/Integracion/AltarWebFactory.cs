using AltarWeb.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AltarWeb.Tests.Integracion;

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
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
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
