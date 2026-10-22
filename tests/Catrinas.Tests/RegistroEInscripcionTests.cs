using System.Net;
using Catrinas.Tests.Infraestructura;
using Catrinas.Web.Modelos;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Concurso.Core.Identidad;

namespace Catrinas.Tests;

[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class RegistroTests
{
    private readonly CatrinasFactory _f;
    public RegistroTests(CatrinasFactory f) => _f = f;

    // 5. Registro local: crea la cuenta (tipo inferido) y abre el portal.
    [Fact]
    public async Task RegistroLocal_CreaCuentaYAbrePanel()
    {
        var n = Escenario.Siguiente();
        var cliente = _f.Cliente();
        Http.Redirige(await Http.SignupAsync(cliente, "Ana Registro", $"18{n:D5}", $"ana.registro{n}@uabc.edu.mx"), "Panel");
        Assert.Contains("Ana Registro", await cliente.GetStringAsync("/Registro/Panel"));

        var r = _f.ConContexto(c => c.Registrantes.AsNoTracking().Single(x => x.Identificador == $"18{n:D5}"));
        Assert.Equal(TipoRegistrante.Alumno, r.Tipo);
        Assert.Equal("Local", r.ProveedorAuth);
        Assert.NotEqual("Participante-2026!", r.PasswordHash);
    }

    // 7. Restricciones de identificador y correo.
    [Theory]
    [InlineData("123456", "uno@uabc.edu.mx")]
    [InlineData("12345678", "dos@uabc.edu.mx")]
    [InlineData("12a4567", "tres@uabc.edu.mx")]
    [InlineData("1234567", "cuatro@gmail.com")]
    public async Task IdentificadorOCorreoInvalido_SeRechaza(string identificador, string correo)
    {
        var respuesta = await Http.SignupAsync(_f.Cliente(), "Dato Invalido", identificador, correo);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode); // vuelve a mostrar el formulario con errores
        Assert.False(_f.ConContexto(c => c.Registrantes.Any(r => r.CorreoInstitucional == correo)));
    }

    [Fact]
    public async Task Identificador5Digitos_EsMaestroOAdministrativo()
    {
        var n = Escenario.Siguiente() % 10000;
        Http.Redirige(await Http.SignupAsync(_f.Cliente(), "Maestro Tipo", $"6{n:D4}", $"maestro.tipo{n}@uabc.edu.mx"), "Panel");
        Http.Redirige(await Http.SignupAsync(_f.Cliente(), "Admin Tipo", $"7{n:D4}", $"admin.tipo{n}@uabc.edu.mx", extra: ("TipoPersonal", "Administrativo")), "Panel");
        Assert.Equal(TipoRegistrante.Maestro, _f.ConContexto(c => c.Registrantes.Single(r => r.Identificador == $"6{n:D4}").Tipo));
        Assert.Equal(TipoRegistrante.Administrativo, _f.ConContexto(c => c.Registrantes.Single(r => r.Identificador == $"7{n:D4}").Tipo));
    }

    // 8. Duplicados de identificador o correo (con distinto casing) no crean otra cuenta.
    [Fact]
    public async Task Duplicados_SeRechazan()
    {
        var n = Escenario.Siguiente();
        Http.Redirige(await Http.SignupAsync(_f.Cliente(), "Original", $"19{n:D5}", $"original{n}@uabc.edu.mx"), "Panel");

        Assert.Equal(HttpStatusCode.OK, (await Http.SignupAsync(_f.Cliente(), "Copia Id", $"19{n:D5}", $"otro{n}@uabc.edu.mx")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Http.SignupAsync(_f.Cliente(), "Copia Correo", $"18{n:D5}", $"  ORIGINAL{n}@UABC.EDU.MX ")).StatusCode);
        Assert.Equal(1, _f.ConContexto(c => c.Registrantes.Count(r => r.Identificador == $"19{n:D5}" || r.CorreoInstitucional == $"original{n}@uabc.edu.mx")));
    }

    // 9. Solicitud de jurado: queda pendiente, no entra hasta que la administracion la aprueba.
    [Fact]
    public async Task SolicitudDeJurado_PendienteHastaAprobacion()
    {
        var n = Escenario.Siguiente();
        var correo = $"jurado.solicitud{n}@uabc.edu.mx";
        Http.Redirige(await Http.PostAsync(_f.Cliente(), "/Registro/SolicitarJurado",
            ("NombreCompleto", "Jurado Solicitante"), ("CorreoInstitucional", correo), ("Identificador", "54321"),
            ("Password", "Solicitud-2026!"), ("ConfirmarPassword", "Solicitud-2026!")), "tab=jurado");

        var solicitante = _f.Cliente();
        var pendiente = await Http.LoginJuradoAsync(solicitante, correo, "Solicitud-2026!");
        Assert.Contains("pendiente de aprobación", await Http.AvisoDeLoginAsync(solicitante, pendiente));

        var admin = await Escenario.JuradoAsync(_f, admin: true);
        var id = _f.ConContexto(c => c.Jueces.Single(j => j.CorreoInstitucional == correo).Id);
        Http.Redirige(await Http.PostDesdeAsync(admin, "/Admin/Jurado", "/Admin/AprobarJuez", ("id", id.ToString())), "Jurado");

        Http.Redirige(await Http.LoginJuradoAsync(solicitante, correo, "Solicitud-2026!"), "Evaluacion");
    }

    // 6. Google: Catrinas usa la validacion endurecida de Core (email_verified + hd) y mapea esos claims;
    //    completar registro sin identidad verificada server-side no crea cuenta.
    [Fact]
    public async Task Google_UsaValidacionDeCore_YNoAceptaIdentidadDelCliente()
    {
        var google = _f.Services.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get("Google");
        Assert.Equal("/Registro/google-callback", google.CallbackPath);
        Assert.Contains(google.ClaimActions, a => a.ClaimType == GoogleInstitucionalExtensions.ClaimEmailVerificado);
        Assert.Contains(google.ClaimActions, a => a.ClaimType == GoogleInstitucionalExtensions.ClaimDominioWorkspace);
        Assert.True(_f.Services.GetRequiredService<IOptions<ValidacionGoogleOptions>>().Value.ExigirDominioWorkspace);

        var challenge = await _f.Cliente().GetAsync("/Registro/GoogleLogin?intent=participante");
        Assert.StartsWith("https://accounts.google.com/", challenge.Headers.Location!.ToString());
        Assert.Contains(Uri.EscapeDataString(CatrinasFactory.Host + "/Registro/google-callback"), challenge.Headers.Location!.ToString());

        var intruso = _f.Cliente();
        var intento = await Http.PostAsync(intruso, "/Registro/CompletarGoogle",
            ("NombreCompleto", "Intruso"), ("CorreoInstitucional", "intruso.catrinas@uabc.edu.mx"), ("Identificador", "1999999"));
        Assert.Contains("sesión de Google expiró", await Http.AvisoDeLoginAsync(intruso, intento));
        Assert.False(_f.ConContexto(c => c.Registrantes.Any(r => r.CorreoInstitucional == "intruso.catrinas@uabc.edu.mx")));
    }
}

[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class InscripcionTests
{
    private readonly CatrinasFactory _f;
    public InscripcionTests(CatrinasFactory f) => _f = f;

    // 10 y 12. Creacion con responsable como integrante; integrantes; exactamente un Personificador.
    [Fact]
    public async Task Creacion_Integrantes_YUnSoloPersonificador()
    {
        var e = await Escenario.CrearAsync(_f, conMaestro: true);
        var i = _f.ConContexto(c => c.Inscripciones.AsNoTracking().Include(x => x.Miembros).Single(x => x.Id == e.InscripcionId));
        Assert.Equal(2, i.Miembros.Count);
        Assert.NotNull(i.MaestroEncargadoId);
        Assert.Single(i.Miembros, m => m.Rol == RolInscripcion.Personificador && m.RegistranteId == e.IntegranteId);

        // Cambiar de Personificador deja exactamente uno.
        Http.Redirige(await Http.PostDesdeAsync(e.Responsable, "/Inscripcion/Editar", "/Inscripcion/DesignarPersonificador", ("registranteId", e.ResponsableId.ToString())), "Editar");
        var miembros = _f.ConContexto(c => c.Miembros.AsNoTracking().Where(m => m.InscripcionId == e.InscripcionId).ToList());
        Assert.Single(miembros, m => m.Rol == RolInscripcion.Personificador && m.RegistranteId == e.ResponsableId);

        // El responsable no puede quitarse; si puede quitar a otro integrante.
        await Http.PostDesdeAsync(e.Responsable, "/Inscripcion/Editar", "/Inscripcion/QuitarIntegrante", ("registranteId", e.ResponsableId.ToString()));
        Assert.True(_f.ConContexto(c => c.Miembros.Any(m => m.RegistranteId == e.ResponsableId)));
        await Http.PostDesdeAsync(e.Responsable, "/Inscripcion/Editar", "/Inscripcion/QuitarIntegrante", ("registranteId", e.IntegranteId.ToString()));
        Assert.False(_f.ConContexto(c => c.Miembros.Any(m => m.RegistranteId == e.IntegranteId)));
    }

    // La BD impide dos Personificadores aunque se salte la logica de la aplicacion.
    [Fact]
    public async Task BaseDeDatos_ImpideDosPersonificadores()
    {
        var e = await Escenario.CrearAsync(_f);
        await Assert.ThrowsAsync<DbUpdateException>(() => _f.ConContextoAsync(async c =>
        {
            var responsable = await c.Miembros.SingleAsync(m => m.InscripcionId == e.InscripcionId && m.RegistranteId == e.ResponsableId);
            responsable.Rol = RolInscripcion.Personificador;
            await c.SaveChangesAsync();
        }));
    }

    // 11. Edicion por el responsable; otro integrante no puede administrarla.
    [Fact]
    public async Task Edicion_SoloPorElResponsable()
    {
        var e = await Escenario.CrearAsync(_f, carreraId: 2);
        Http.Redirige(await Http.PostDesdeAsync(e.Responsable, "/Inscripcion/Editar", "/Inscripcion/Editar",
            ("Nombre", e.Nombre + " editada"), ("CarreraId", "3")), "Editar");
        var i = _f.ConContexto(c => c.Inscripciones.AsNoTracking().Single(x => x.Id == e.InscripcionId));
        Assert.Equal(e.Nombre + " editada", i.Nombre);
        Assert.Equal(3, i.CarreraId);

        var intento = await e.Integrante.GetAsync("/Inscripcion/Editar");
        Http.Redirige(intento, "Panel");
    }

    // 13. Una inscripcion por persona y periodo; fechas limite de inscripcion y de requisitos.
    [Fact]
    public async Task RestriccionesPorPeriodo()
    {
        var a = await Escenario.CrearAsync(_f);
        var b = await Escenario.CrearAsync(_f);

        // El integrante de A no puede sumarse a B ni crear otra inscripcion.
        var integranteA = _f.ConContexto(c => c.Registrantes.Single(r => r.Id == a.IntegranteId).Identificador);
        await Http.PostDesdeAsync(b.Responsable, "/Inscripcion/Editar", "/Inscripcion/AgregarIntegrante", ("identificador", integranteA));
        Assert.Equal(1, _f.ConContexto(c => c.Miembros.Count(m => m.RegistranteId == a.IntegranteId)));
        Http.Redirige(await a.Integrante.GetAsync("/Inscripcion/Crear"), "Panel");

        // Fecha limite de requisitos vencida: ya no se edita ni se agregan integrantes.
        var periodo = Concurso.Core.Fechas.PeriodoHelper.ObtenerPeriodoActual();
        await GuardarConfiguracionAsync(periodo, inscripcion: null, requisitos: Concurso.Core.Fechas.FechaConcursoHelper.HoyLocal.AddDays(-1).ToDateTime(TimeOnly.MinValue));
        try
        {
            await Http.PostDesdeAsync(b.Responsable, "/Inscripcion/Editar", "/Inscripcion/Editar", ("Nombre", "No debe cambiar"), ("CarreraId", "1"));
            Assert.NotEqual("No debe cambiar", _f.ConContexto(c => c.Inscripciones.Single(i => i.Id == b.InscripcionId).Nombre));

            // Fecha limite de inscripcion vencida: no se crean inscripciones nuevas.
            await GuardarConfiguracionAsync(periodo, inscripcion: Concurso.Core.Fechas.FechaConcursoHelper.HoyLocal.AddDays(-1).ToDateTime(TimeOnly.MinValue), requisitos: null);
            var n = Escenario.Siguiente();
            var tarde = _f.Cliente();
            await Http.SignupAsync(tarde, "Llega Tarde", $"15{n:D5}", $"tarde{n}@uabc.edu.mx");
            Http.Redirige(await Http.CrearInscripcionAsync(tarde, $"Tardia {n}", 1), "Panel");
            Assert.False(_f.ConContexto(c => c.Inscripciones.Any(i => i.Nombre == $"Tardia {n}")));
        }
        finally
        {
            await GuardarConfiguracionAsync(periodo, null, null);
        }
    }

    // 14. Referencia opcional a Altares: formato y longitud, sin consultar Altares.
    [Fact]
    public async Task ReferenciaAAltares_SoloFormatoYLongitud()
    {
        var e = await Escenario.CrearAsync(_f);
        async Task<HttpResponseMessage> Guardar(string? id, string? nombre) =>
            await Http.PostDesdeAsync(e.Responsable, "/Inscripcion/Editar", "/Inscripcion/Editar",
                ("Nombre", e.Nombre), ("CarreraId", "1"), ("AltarExternalId", id ?? ""), ("AltarNombreSnapshot", nombre ?? ""));

        Http.Redirige(await Guardar(" alt-2031-1-0007 ", "Equipo Hermano"), "Editar");
        var i = _f.ConContexto(c => c.Inscripciones.AsNoTracking().Single(x => x.Id == e.InscripcionId));
        Assert.Equal("ALT-2031-1-0007", i.AltarExternalId);
        Assert.Equal("Equipo Hermano", i.AltarNombreSnapshot);

        foreach (var invalido in new[] { "ALT-26-2-0001", "XYZ-2026-2-0001", "ALT-2026-3-0001", "ALT-2026-2-00A1", "ALT-2026-2-0001; DROP TABLE Inscripciones" })
        {
            Assert.Equal(HttpStatusCode.OK, (await Guardar(invalido, null)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await Guardar(null, "Nombre sin codigo")).StatusCode);
        Assert.Equal("ALT-2031-1-0007", _f.ConContexto(c => c.Inscripciones.Single(x => x.Id == e.InscripcionId).AltarExternalId));

        // Quitar la referencia es valido.
        Http.Redirige(await Guardar(null, null), "Editar");
        Assert.Null(_f.ConContexto(c => c.Inscripciones.Single(x => x.Id == e.InscripcionId).AltarExternalId));

        // La BD tambien rechaza un formato invalido (CHECK).
        await Assert.ThrowsAsync<DbUpdateException>(() => _f.ConContextoAsync(async c =>
        {
            (await c.Inscripciones.SingleAsync(x => x.Id == e.InscripcionId)).AltarExternalId = "NO-ES-ALTAR";
            await c.SaveChangesAsync();
        }));
    }

    // 15. Concurrencia: la misma persona agregada a dos inscripciones a la vez queda solo en una.
    [Fact]
    public async Task AltaConcurrenteDeLaMismaPersona_SoloUnaGana()
    {
        var a = await Escenario.CrearAsync(_f, conPersonificador: false);
        var b = await Escenario.CrearAsync(_f, conPersonificador: false);
        var n = Escenario.Siguiente();
        await Http.SignupAsync(_f.Cliente(), "Disputada", $"13{n:D5}", $"disputada{n}@uabc.edu.mx");

        var tokenA = await Http.TokenAsync(a.Responsable, "/Inscripcion/Editar");
        var tokenB = await Http.TokenAsync(b.Responsable, "/Inscripcion/Editar");
        Task<HttpResponseMessage> Agregar(HttpClient c, string token) => c.PostAsync("/Inscripcion/AgregarIntegrante",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["identificador"] = $"13{n:D5}", ["__RequestVerificationToken"] = token }));

        var respuestas = await Task.WhenAll(Agregar(a.Responsable, tokenA), Agregar(b.Responsable, tokenB));
        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode)); // nunca un 500
        Assert.Equal(1, _f.ConContexto(c => c.Miembros.Count(m => m.Registrante.Identificador == $"13{n:D5}")));
    }

    private Task GuardarConfiguracionAsync(string periodo, DateTime? inscripcion, DateTime? requisitos) => _f.ConContextoAsync(async c =>
    {
        var config = await c.Configuraciones.FirstOrDefaultAsync(x => x.Periodo == periodo);
        if (config == null)
        {
            config = new ConfiguracionPeriodo { Periodo = periodo };
            c.Configuraciones.Add(config);
        }
        config.FechaLimiteInscripcion = inscripcion;
        config.FechaLimiteRequisitos = requisitos;
        await c.SaveChangesAsync();
    });
}
