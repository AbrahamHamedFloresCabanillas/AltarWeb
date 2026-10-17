using System.Diagnostics;
using System.Net;
using System.Reflection;
using Catrinas.Tests.Infraestructura;
using Catrinas.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Tests;

public class AutorizacionPorConvencionTests
{
    // 28. Ninguna accion de Catrinas queda sin declarar quien puede usarla (mitigacion de SEC-08).
    [Fact]
    public void TodaAccion_DeclaraSuAcceso()
    {
        var marcas = new[] { typeof(AccesoPublicoAttribute), typeof(SoloJuradoAttribute), typeof(SoloAdminAttribute), typeof(SoloParticipanteAttribute) };
        var controladores = typeof(Program).Assembly.GetTypes().Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        Assert.NotEmpty(controladores);

        var sinDeclarar = controladores
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null)
                .Where(m => !marcas.Any(a => m.IsDefined(a) || c.IsDefined(a)))
                .Select(m => $"{c.Name}.{m.Name}"))
            .ToList();

        Assert.Empty(sinDeclarar);
    }
}

[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class SeguridadTests
{
    private readonly CatrinasFactory _f;
    public SeguridadTests(CatrinasFactory f) => _f = f;

    // 28. Endpoints protegidos sin sesion.
    [Theory]
    [InlineData("/Evaluacion/Index", "tab=jurado")]
    [InlineData("/Evaluacion/Evaluar?inscripcionId=1", "tab=jurado")]
    [InlineData("/Evaluacion/Detalle/1", "tab=jurado")]
    [InlineData("/Evaluacion/Ranking", "tab=jurado")]
    [InlineData("/Admin/Jurado", "tab=jurado")]
    [InlineData("/Admin/Participantes", "tab=jurado")]
    [InlineData("/Admin/Configuracion", "tab=jurado")]
    [InlineData("/Admin/Diagnostico", "tab=jurado")]
    [InlineData("/Constancia/Grupal?evaluacionId=1", "tab=jurado")]
    [InlineData("/Constancia/Individuales?evaluacionId=1", "tab=jurado")]
    [InlineData("/Registro/Panel", "/")]
    [InlineData("/Inscripcion/Crear", "/")]
    [InlineData("/Inscripcion/Editar", "/")]
    [InlineData("/Constancia/Mia", "/")]
    public async Task SinSesion_Redirige(string ruta, string destino)
    {
        var r = await _f.Cliente().GetAsync(ruta);
        Http.Redirige(r, destino);
    }

    // Diagnostico del gate R6: solo admin, refleja la IP de la conexion y no expone cookies.
    [Fact]
    public async Task Diagnostico_SoloAdmin_SinCookies()
    {
        var juez = await Escenario.JuradoAsync(_f);
        Http.Redirige(await juez.GetAsync("/Admin/Diagnostico"), "Evaluacion");

        var admin = await Escenario.JuradoAsync(_f, admin: true);
        var peticion = new HttpRequestMessage(HttpMethod.Get, "/Admin/Diagnostico");
        peticion.Headers.Add(CatrinasFactory.CabeceraIpRemota, "198.51.100.77");
        peticion.Headers.Add("X-Forwarded-For", "192.0.2.10");
        var json = await (await admin.SendAsync(peticion)).Content.ReadAsStringAsync();
        Assert.Contains("\"red.remoteIpAddress\":\"198.51.100.77\"", json);
        Assert.Contains("cabecera.X-Forwarded-For", json);
        Assert.Contains("memoria.workingSetMB", json);
        Assert.DoesNotContain(".Catrinas.Sesion", json);
        Assert.DoesNotContain("Cookie", json);
    }

    // 29. Antiforgery en POST.
    [Fact]
    public async Task PostSinToken_Responde400()
    {
        var r = await _f.Cliente().PostAsync("/Registro/Signup", new FormUrlEncodedContent(new Dictionary<string, string> { ["NombreCompleto"] = "X" }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    // 30. Roles: un juez no administra; un participante no evalua; la cuenta semilla debe cambiar password.
    [Fact]
    public async Task Roles()
    {
        var juez = await Escenario.JuradoAsync(_f);
        Http.Redirige(await juez.GetAsync("/Admin/Jurado"), "Evaluacion");
        Http.Redirige(await Http.PostAsync(juez, "/Admin/AprobarJuez", ("id", "1")), "Evaluacion");

        var e = await Escenario.CrearAsync(_f);
        Http.Redirige(await e.Responsable.GetAsync("/Evaluacion/Index"), "tab=jurado");

        var semilla = _f.Cliente();
        Http.Redirige(await Http.LoginJuradoAsync(semilla, CatrinasFactory.AdminUsuario, CatrinasFactory.AdminPassword), "CambiarPasswordObligatorio");
        Http.Redirige(await semilla.GetAsync("/Admin/Configuracion"), "CambiarPasswordObligatorio");
    }

    // Revision de seguridad F3 (M2): desactivar una cuenta corta su sesion abierta en la siguiente peticion.
    [Fact]
    public async Task CuentaDesactivada_PierdeSuSesionAbierta()
    {
        var juez = await Escenario.JuradoAsync(_f);
        Assert.Equal(HttpStatusCode.OK, (await juez.GetAsync("/Evaluacion/Index")).StatusCode);
        var e = await Escenario.CrearAsync(_f);
        Assert.Equal(HttpStatusCode.OK, (await e.Responsable.GetAsync("/Inscripcion/Editar")).StatusCode);

        await _f.ConContextoAsync(async c =>
        {
            foreach (var j in await c.Jueces.Where(j => j.Usuario!.StartsWith("jurado-")).ToListAsync()) j.Activo = false;
            (await c.Registrantes.SingleAsync(r => r.Id == e.ResponsableId)).Activo = false;
            await c.SaveChangesAsync();
        });

        Http.Redirige(await juez.GetAsync("/Evaluacion/Index"), "tab=jurado");
        Http.Redirige(await e.Responsable.GetAsync("/Inscripcion/Editar"), "/");
        // La sesion quedo limpia: reactivar la cuenta no la revive.
        await _f.ConContextoAsync(async c =>
        {
            (await c.Registrantes.SingleAsync(r => r.Id == e.ResponsableId)).Activo = true;
            await c.SaveChangesAsync();
        });
        Http.Redirige(await e.Responsable.GetAsync("/Inscripcion/Editar"), "/");
    }

    // 31. Rate limiting por cuenta (Core) tambien en Catrinas: el 6.o intento fallido recibe 429.
    [Fact]
    public async Task RateLimiting_PorCuenta()
    {
        var cliente = _f.Cliente();
        for (var i = 0; i < 5; i++) Http.Redirige(await Http.LoginJuradoAsync(cliente, "cuenta-limitada-catrinas", "mala"), "tab=jurado");
        var bloqueada = await Http.LoginJuradoAsync(cliente, "  CUENTA-LIMITADA-CATRINAS ", "mala");
        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
        Assert.NotNull(bloqueada.Headers.RetryAfter);

        for (var i = 0; i < 5; i++) await Http.PostAsync(cliente, "/Registro/Login", ("CorreoInstitucional", "limitado@uabc.edu.mx"), ("Password", "mala"));
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Http.PostAsync(cliente, "/Registro/Login", ("CorreoInstitucional", "limitado@uabc.edu.mx"), ("Password", "mala"))).StatusCode);
        Assert.DoesNotContain(_f.Logs, l => l.Contains("mala"));
    }

    // 32. Sin mass assignment: campos ajenos al ViewModel se ignoran.
    [Fact]
    public async Task CamposNoPermitidos_SeIgnoran()
    {
        var n = Escenario.Siguiente();
        var cliente = _f.Cliente();
        Http.Redirige(await Http.SignupAsync(cliente, "Sin Privilegios", $"11{n:D5}", $"sin.priv{n}@uabc.edu.mx", extra: new[]
        {
            ("Activo", "false"), ("ProveedorAuth", "Google"), ("Id", "999999"), ("Tipo", "Maestro")
        }), "Panel");
        var r = _f.ConContexto(c => c.Registrantes.AsNoTracking().Single(x => x.CorreoInstitucional == $"sin.priv{n}@uabc.edu.mx"));
        Assert.True(r.Activo);
        Assert.Equal("Local", r.ProveedorAuth);
        Assert.Equal(Catrinas.Web.Modelos.TipoRegistrante.Alumno, r.Tipo);

        Http.Redirige(await Http.CrearInscripcionAsync(cliente, $"Inscripcion Limpia {n}", 1,
            ("Periodo", "1999-1"), ("ResponsableId", "1"), ("Activo", "false")), "Editar");
        var i = _f.ConContexto(c => c.Inscripciones.AsNoTracking().Single(x => x.Nombre == $"Inscripcion Limpia {n}"));
        Assert.Equal(Concurso.Core.Fechas.PeriodoHelper.ObtenerPeriodoActual(), i.Periodo);
        Assert.Equal(r.Id, i.ResponsableId);
        Assert.True(i.Activo);
    }

    // 33. Entradas codificadas: un nombre con HTML se muestra escapado.
    [Fact]
    public async Task Entradas_SeMuestranCodificadas()
    {
        var e = await Escenario.CrearAsync(_f, nombre: $"<script>alert('x')</script> {Escenario.Siguiente()}");
        var panel = await e.Responsable.GetStringAsync("/Registro/Panel");
        Assert.DoesNotContain("<script>alert('x')</script>", panel);
        Assert.Contains("&lt;script&gt;", panel);

        var jurado = await Escenario.JuradoAsync(_f);
        Assert.DoesNotContain("<script>alert('x')</script>", await jurado.GetStringAsync("/Evaluacion/Index"));
    }

    // 34. Secretos fuera del repositorio: appsettings.json de Catrinas esta ignorado y no versionado.
    [Fact]
    public void Secretos_FueraDelRepositorio()
    {
        var raiz = BuscarRaizRepositorio();
        Assert.Equal(0, Git(raiz, "check-ignore -q src/Catrinas.Web/appsettings.json").Codigo);
        var versionados = Git(raiz, "ls-files src/Catrinas.Web").Salida;
        Assert.DoesNotContain("appsettings.json", versionados.Split('\n').Select(l => l.Trim().Split('/').Last()));
        Assert.DoesNotContain("appsettings.Development.json", versionados);
    }

    private static string BuscarRaizRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró el repositorio.");
    }

    private static (int Codigo, string Salida) Git(string directorio, string argumentos)
    {
        using var proceso = Process.Start(new ProcessStartInfo("git", argumentos)
        {
            WorkingDirectory = directorio,
            RedirectStandardOutput = true,
            UseShellExecute = false
        })!;
        var salida = proceso.StandardOutput.ReadToEnd();
        proceso.WaitForExit();
        return (proceso.ExitCode, salida);
    }
}
