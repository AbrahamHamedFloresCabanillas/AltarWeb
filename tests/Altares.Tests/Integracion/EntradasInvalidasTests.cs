using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using AltarWeb.Models.Registro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Altares.Tests.Integracion;

// M1 y M2 (pruebas adversariales 2026-10-21). M1: formularios con un campo ausente, de solo espacios o con un
// catalogo inexistente terminaban en error 500 (NullReferenceException o violacion de llave foranea). M2: los
// datos del participante no tenian limite de longitud (nombre de 5,000 caracteres, constancia de 9 paginas).
// Ahora se responden con el formulario y su mensaje, con los mismos limites que Catrinas.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class EntradasInvalidasTests
{
    private const string Password = "Entradas-Invalidas-2026!";
    private static int _contador = 3000;

    private readonly AltarWebFactory _factory;
    public EntradasInvalidasTests(AltarWebFactory factory) => _factory = factory;

    private static int Siguiente() => Interlocked.Increment(ref _contador);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rutaFormulario, string accion, params (string, string)[] campos)
    {
        var html = await cliente.GetStringAsync(rutaFormulario);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(token.Success, $"Sin token antiforgery en {rutaFormulario}");
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = token.Groups[1].Value;
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }

    private T Ctx<T>(Func<AltarDbContext, T> consulta)
    {
        using var scope = _factory.Services.CreateScope();
        return consulta(scope.ServiceProvider.GetRequiredService<AltarDbContext>());
    }

    private int PrimeraCarrera() => Ctx(c => c.CatalogoCarreras.Where(x => x.Activo).OrderBy(x => x.Id).Select(x => x.Id).First());

    // Formulario aceptado (redirige) o rechazado con la vista (200); nunca un 500.
    private static void AssertSin500(HttpResponseMessage respuesta) =>
        Assert.NotEqual(HttpStatusCode.InternalServerError, respuesta.StatusCode);

    private static (string, string)[] Signup(int n, params (string, string)[] cambios)
    {
        var campos = new Dictionary<string, string>
        {
            ["NombreCompleto"] = $"Participante Entradas {n}", ["Identificador"] = $"17{n:D5}",
            ["CorreoInstitucional"] = $"entradas.{n}@uabc.edu.mx", ["Password"] = Password, ["ConfirmarPassword"] = Password
        };
        foreach (var (campo, valor) in cambios)
        {
            if (valor == null) campos.Remove(campo); else campos[campo] = valor;
        }
        return campos.Select(c => (c.Key, c.Value)).ToArray();
    }

    private bool ExisteRegistrante(int n) => Ctx(c => c.Registrantes.IgnoreQueryFilters().Any(r => r.CorreoInstitucional == $"entradas.{n}@uabc.edu.mx"));

    public static TheoryData<string, string?> SignupInvalidos => new()
    {
        { "NombreCompleto", "   " },
        { "Identificador", "   " },
        { "CorreoInstitucional", "   " },
        { "CatalogoCarreraId", "9999" },
        { "CatalogoGeneroId", "9999" },
        { "NombreCompleto", new string('A', 201) },
        { "Telefono", new string('5', 21) },
        { "AutodescripcionCultural", new string('x', 501) }
    };

    [Theory]
    [MemberData(nameof(SignupInvalidos))]
    public async Task Signup_ConDatoInvalido_NoDa500NiGuarda(string campo, string? valor)
    {
        var n = Siguiente();
        var respuesta = await PostAsync(_factory.CrearCliente(), "/Registro/Signup", "/Registro/Signup", Signup(n, (campo, valor!)));

        AssertSin500(respuesta);
        Assert.False(ExisteRegistrante(n), $"Se guardo el registrante con {campo} invalido");
    }

    [Fact]
    public async Task Signup_EnElLimiteDeLongitud_SeAcepta()
    {
        var n = Siguiente();
        var respuesta = await PostAsync(_factory.CrearCliente(), "/Registro/Signup", "/Registro/Signup",
            Signup(n, ("NombreCompleto", new string('A', 200)), ("Telefono", new string('5', 20)), ("AutodescripcionCultural", new string('x', 500))));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.True(ExisteRegistrante(n));
    }

    [Fact]
    public async Task SignupJuez_IdentificadorDeSoloEspacios_NoDa500()
    {
        var n = Siguiente();
        var respuesta = await PostAsync(_factory.CrearCliente(), "/Registro/SignupJuez", "/Registro/SignupJuez",
            ("NombreCompleto", "Juez Entradas"), ("CorreoInstitucional", $"juez.entradas.{n}@uabc.edu.mx"), ("Identificador", "   "),
            ("Password", Password), ("ConfirmarPassword", Password));
        AssertSin500(respuesta);
    }

    private async Task<HttpClient> ParticipanteAsync()
    {
        var n = Siguiente();
        var cliente = _factory.CrearCliente();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(cliente, "/Registro/Signup", "/Registro/Signup", Signup(n))).StatusCode);
        return cliente;
    }

    private (string, string)[] Equipo(int n, params (string, string)[] cambios)
    {
        var campos = new Dictionary<string, string>
        {
            ["Nombre"] = $"Equipo Entradas {n}", ["NombreAltar"] = "Altar de entradas", ["CarreraId"] = PrimeraCarrera().ToString(),
            ["MaestroEncargadoIdentificador"] = "54321", ["UbicacionAltar"] = "Explanada",
            ["DifuntoNombre"] = "Difunto de entradas", ["DifuntoFechaDefuncion"] = "2020-01-15"
        };
        foreach (var (campo, valor) in cambios) campos[campo] = valor;
        return campos.Select(c => (c.Key, c.Value)).ToArray();
    }

    public static TheoryData<string, string> EquipoInvalidos => new()
    {
        { "Nombre", "   " },
        { "CarreraId", "9999" },
        { "Nombre", new string('N', 3000) },
        { "Nombre", new string('N', 151) },
        { "NombreAltar", new string('A', 151) },
        { "DifuntoNombre", new string('D', 201) },
        { "UbicacionAltar", new string('U', 201) }
    };

    [Theory]
    [MemberData(nameof(EquipoInvalidos))]
    public async Task CrearEquipo_ConDatoInvalido_NoDa500NiGuarda(string campo, string valor)
    {
        var participante = await ParticipanteAsync();
        var n = Siguiente();

        var respuesta = await PostAsync(participante, "/Registro/CrearEquipo", "/Registro/CrearEquipo", Equipo(n, (campo, valor)));

        AssertSin500(respuesta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False(Ctx(c => c.EquiposConcurso.IgnoreQueryFilters().Any(e => e.NombreAltar == "Altar de entradas" && e.Nombre == $"Equipo Entradas {n}")));
    }

    [Theory]
    [MemberData(nameof(EquipoInvalidos))]
    public async Task Ficha_ConDatoInvalido_NoDa500NiCambia(string campo, string valor)
    {
        var participante = await ParticipanteAsync();
        var n = Siguiente();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(participante, "/Registro/CrearEquipo", "/Registro/CrearEquipo", Equipo(n))).StatusCode);
        var equipoId = Ctx(c => c.EquiposConcurso.Single(e => e.Nombre == $"Equipo Entradas {n}").Id);

        var respuesta = await PostAsync(participante, "/Registro/Ficha", "/Registro/Ficha", Equipo(n, (campo, valor)).Append(("EquipoId", equipoId.ToString())).ToArray());

        AssertSin500(respuesta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(PrimeraCarrera(), Ctx(c => c.EquiposConcurso.Single(e => e.Id == equipoId).CarreraId));
    }

    [Fact]
    public async Task AgregarIntegrante_SinIdentificador_NoDa500()
    {
        var participante = await ParticipanteAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(participante, "/Registro/CrearEquipo", "/Registro/CrearEquipo", Equipo(Siguiente()))).StatusCode);

        // Un campo de solo espacios llega como null (uno ausente conserva el valor por defecto y no fallaba).
        var respuesta = await PostAsync(participante, "/Registro/Ficha", "/Registro/AgregarIntegrante", ("identificador", "   "));

        AssertSin500(respuesta);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var usuario = $"admin-entradas-{Siguiente()}";
        await _factory.CrearJuezAsync(usuario, Password, rol: "Admin");
        var admin = _factory.CrearCliente();
        Assert.Contains("Historial", (await PostAsync(admin, "/Registro/Login", "/Acceso/Login", ("usuario", usuario), ("password", Password))).Headers.Location!.OriginalString);
        return admin;
    }

    [Theory]
    [MemberData(nameof(EquipoInvalidos))]
    public async Task AdminEditarEquipo_ConDatoInvalido_NoDa500(string campo, string valor)
    {
        var participante = await ParticipanteAsync();
        var n = Siguiente();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(participante, "/Registro/CrearEquipo", "/Registro/CrearEquipo", Equipo(n))).StatusCode);
        var equipoId = Ctx(c => c.EquiposConcurso.Single(e => e.Nombre == $"Equipo Entradas {n}").Id);
        var admin = await AdminAsync();

        var respuesta = await PostAsync(admin, $"/AltarAdmin/EditarEquipo/{equipoId}", "/AltarAdmin/EditarEquipo",
            Equipo(n, (campo, valor)).Append(("Id", equipoId.ToString())).ToArray());

        AssertSin500(respuesta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    public static TheoryData<string, string?> RegistranteAdminInvalidos => new()
    {
        { "Identificador", "   " },
        { "CorreoInstitucional", "   " },
        { "CatalogoCarreraId", "9999" },
        { "CatalogoGeneroId", "9999" },
        { "NombreCompleto", new string('A', 201) }
    };

    [Theory]
    [MemberData(nameof(RegistranteAdminInvalidos))]
    public async Task AdminEditarRegistrante_ConDatoInvalido_NoDa500NiCambia(string campo, string? valor)
    {
        var n = Siguiente();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(_factory.CrearCliente(), "/Registro/Signup", "/Registro/Signup", Signup(n))).StatusCode);
        var id = Ctx(c => c.Registrantes.Single(r => r.CorreoInstitucional == $"entradas.{n}@uabc.edu.mx").Id);
        var admin = await AdminAsync();

        var campos = new Dictionary<string, string>
        {
            ["Id"] = id.ToString(), ["Tipo"] = nameof(TipoRegistrante.Alumno), ["NombreCompleto"] = "Nombre corregido",
            ["Identificador"] = $"17{n:D5}", ["CorreoInstitucional"] = $"entradas.{n}@uabc.edu.mx"
        };
        if (valor == null) campos.Remove(campo); else campos[campo] = valor;
        var respuesta = await PostAsync(admin, $"/AltarAdmin/EditarRegistrante/{id}", "/AltarAdmin/EditarRegistrante",
            campos.Select(c => (c.Key, c.Value)).ToArray());

        AssertSin500(respuesta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal($"Participante Entradas {n}", Ctx(c => c.Registrantes.Single(r => r.Id == id).NombreCompleto));
    }
}
