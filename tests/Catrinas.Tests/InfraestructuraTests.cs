extern alias altares;

using System.Net;
using System.Reflection;
using Catrinas.Tests.Infraestructura;
using Catrinas.Web.Datos;
using Catrinas.Web.Modelos;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Catrinas.Tests;

[Trait("Category", "Integracion")]
public class InfraestructuraTests
{
    // 1. Una base SQL Server completamente vacia se convierte en una instancia funcional ejecutando
    //    UNICAMENTE las migraciones de Catrinas.Web.
    [Fact]
    public async Task BaseVacia_SoloConMigracionesDeCatrinas_QuedaFuncional()
    {
        using var factory = new CatrinasFactory();
        await using (var maestra = new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True"))
        {
            await maestra.OpenAsync();
            await using var crear = new SqlCommand($"CREATE DATABASE [{factory.NombreBase}]", maestra);
            await crear.ExecuteNonQueryAsync();
        }

        // Base vacia: ni una tabla.
        await using (var conexion = new SqlConnection(factory.CadenaConexion))
        {
            await conexion.OpenAsync();
            await using var tablas = new SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES", conexion);
            Assert.Equal(0, (int)(await tablas.ExecuteScalarAsync())!);
        }

        // El arranque aplica solo las migraciones de Catrinas y la app queda operativa.
        var cliente = factory.Cliente();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/Registro/Login")).StatusCode);
        Http.Redirige(await Http.SignupAsync(cliente, "Persona Base Vacia", "1400001", "base.vacia@uabc.edu.mx"), "Panel");
        Http.Redirige(await Http.CrearInscripcionAsync(cliente, "Inscripcion Base Vacia", 1), "Inscripcion/Editar");

        factory.ConContexto(c =>
        {
            Assert.Equal(13, c.Carreras.Count());
            Assert.Equal(5, c.Generos.Count());
            Assert.Single(c.Jueces.Where(j => j.Usuario == CatrinasFactory.AdminUsuario && j.Rol == RolesJuez.Admin && j.DebeCambiarPassword));
            Assert.Empty(c.Database.GetPendingMigrations());
            Assert.Equal(c.Database.GetMigrations(), c.Database.GetAppliedMigrations());
            return 0;
        });
    }

    // 2. El modelo de Catrinas no contiene entidades de Altares ni depende de su ensamblado.
    [Fact]
    public void ModeloDeCatrinas_NoContieneEntidadesDeAltares()
    {
        using var context = new CatrinasDbContext(new DbContextOptionsBuilder<CatrinasDbContext>().UseSqlServer("Server=sin-conexion").Options);
        var entidades = context.Model.GetEntityTypes().Select(e => e.ClrType).ToList();

        Assert.All(entidades, t => Assert.Equal("Catrinas.Web.Modelos", t.Namespace));
        var nombres = entidades.Select(t => t.Name).ToList();
        foreach (var exclusivaDeAltares in new[] { "Difunto", "Elemento", "ElementoEvaluado", "Equipo", "RegistranteEquipo", "Evaluacion", "EvaluacionIntegrante", "Alumno", "Integrante", "ContadorCodigoPublico" })
        {
            Assert.DoesNotContain(exclusivaDeAltares, nombres);
        }

        var referencias = typeof(CatrinasDbContext).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.DoesNotContain("Altares.Web", referencias);
        Assert.Contains("Concurso.Core", referencias);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    // 3. Catrinas funciona sin Altares: no hay cadena de conexion de Altares, y una referencia a un equipo
    //    de Altares inexistente se guarda igual (referencia blanda, sin consulta remota).
    [Fact]
    public async Task Catrinas_FuncionaConAltaresAusente()
    {
        using var factory = new CatrinasFactory();
        Environment.SetEnvironmentVariable("ConnectionStrings__AltarContext", null);
        var configuracion = factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        Assert.Null(configuracion.GetConnectionString("AltarContext"));
        Assert.Null(configuracion.GetConnectionString("AltarWebContext"));

        var escenario = await Escenario.CrearAsync(factory, extraInscripcion: new[] { ("AltarExternalId", "alt-2026-2-9999"), ("AltarNombreSnapshot", "Equipo que no existe") });
        var inscripcion = factory.ConContexto(c => c.Inscripciones.AsNoTracking().Single(i => i.Id == escenario.InscripcionId));
        Assert.Equal("ALT-2026-2-9999", inscripcion.AltarExternalId);

        var jurado = await Escenario.JuradoAsync(factory);
        Http.Redirige(await Http.GuardarEvaluacionAsync(jurado, escenario.InscripcionId, "final", 9, 8, 7, 9, 10), "Detalle");
        Assert.Equal("ALT-2026-2-9999", Escenario.Evaluacion(factory, escenario.InscripcionId)!.SnapshotAltarExternalId);
    }

    // 4. Cookies, llaves y sesiones son propias: una sesion de Altares no autentica en Catrinas.
    [Fact]
    public async Task SesionDeAltares_NoAutenticaEnCatrinas()
    {
        using var catrinas = new CatrinasFactory();
        _ = catrinas.Services; // construye Catrinas con SUS variables antes de que Altares fije las suyas
        using var altaresFactory = new AltaresMinimo();

        var nombreCookieCatrinas = catrinas.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Builder.SessionOptions>>().Value.Cookie.Name;
        var nombreCookieAltares = altaresFactory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Builder.SessionOptions>>().Value.Cookie.Name;
        Assert.Equal(".Catrinas.Sesion", nombreCookieCatrinas);
        Assert.NotEqual(nombreCookieCatrinas, nombreCookieAltares);
        Assert.NotEqual(
            catrinas.Services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator,
            altaresFactory.Services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);

        // Sesion valida de juez en Altares.
        var altares = altaresFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://altares.test") });
        var token = await Http.TokenAsync(altares);
        var login = await altares.PostAsync("/Acceso/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["usuario"] = AltaresMinimo.Admin, ["password"] = AltaresMinimo.Password, ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var cookieAltares = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(nombreCookieAltares + "="));
        var valor = cookieAltares.Split(';')[0].Split('=', 2)[1];

        // El mismo valor, con el nombre de Altares o con el de Catrinas, no autentica en Catrinas.
        foreach (var nombre in new[] { nombreCookieAltares, nombreCookieCatrinas })
        {
            var cliente = catrinas.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri(CatrinasFactory.Host), HandleCookies = false });
            var peticion = new HttpRequestMessage(HttpMethod.Get, "/Evaluacion/Index");
            peticion.Headers.Add("Cookie", $"{nombre}={valor}");
            var respuesta = await cliente.SendAsync(peticion);
            // Sin sesion valida de Catrinas: SoloJurado manda al acceso del jurado ("/?tab=jurado").
            Http.Redirige(respuesta, "tab=jurado");
        }
    }

    // Instancia minima de Altares (con su propia LocalDB) solo para la prueba de aislamiento.
    private sealed class AltaresMinimo : WebApplicationFactory<altares::Program>
    {
        public const string Admin = "admin-altares-aislamiento";
        public const string Password = "Altares-Aislamiento-2026!";
        private readonly string _base = $"AltaresAislamiento_{Guid.NewGuid():N}";

        public AltaresMinimo()
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__AltarContext", $"Server=(localdb)\\MSSQLLocalDB;Database={_base};Trusted_Connection=True");
            Environment.SetEnvironmentVariable("SEED_ADMIN_USUARIO", Admin);
            Environment.SetEnvironmentVariable("SEED_ADMIN_PASSWORD", Password);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Production");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    using var scope = Services.CreateScope();
                    scope.ServiceProvider.GetRequiredService<altares::AltarWeb.Models.AltarDbContext>().Database.EnsureDeleted();
                }
                catch
                {
                }
                Environment.SetEnvironmentVariable("ConnectionStrings__AltarContext", null);
                Environment.SetEnvironmentVariable("SEED_ADMIN_USUARIO", CatrinasFactory.AdminUsuario);
                Environment.SetEnvironmentVariable("SEED_ADMIN_PASSWORD", CatrinasFactory.AdminPassword);
            }
            base.Dispose(disposing);
        }
    }
}
