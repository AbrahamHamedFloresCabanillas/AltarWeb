using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Altares.Tests.Integracion;

// B1 (revision manual del propietario, 2026-10-21): la interfaz decia "identificador" (o "Identif.") donde el
// usuario espera "Matrícula / No. de empleado" (7 digitos alumno, 5 maestro/administrativo).
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class EtiquetaMatriculaTests
{
    private const string Etiqueta = "Matrícula / No. de empleado";
    private const string Password = "Etiqueta-Matricula-2026!";
    private readonly AltarWebFactory _factory;
    public EtiquetaMatriculaTests(AltarWebFactory factory) => _factory = factory;

    // Texto que ve el usuario: sin etiquetas, atributos ni scripts (name="Identificador" no cuenta).
    private static string TextoVisible(string html) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<script[\\s\\S]*?</script>", " "), "<[^>]+>", " "));

    private static void AssertSinIdentificador(string html, bool conEtiqueta = true)
    {
        var texto = TextoVisible(html);
        Assert.DoesNotMatch("(?i)identificador|identif\\.", texto);
        // Los placeholders son atributos: se revisan aparte.
        Assert.DoesNotMatch("(?i)placeholder=\"[^\"]*identificador", html);
        // La etiqueta puede estar en el texto o, en los campos sin etiqueta propia, en el placeholder.
        if (conEtiqueta) Assert.True(texto.Contains(Etiqueta) || WebUtility.HtmlDecode(html).Contains($"placeholder=\"{Etiqueta}"), $"Falta \"{Etiqueta}\"");
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rutaFormulario, string accion, params (string, string)[] campos)
    {
        var html = await cliente.GetStringAsync(rutaFormulario);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = token;
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }

    [Fact]
    public async Task PantallasPublicasYDelParticipante_DicenMatricula()
    {
        var anonimo = _factory.CrearCliente();
        AssertSinIdentificador(await anonimo.GetStringAsync("/Registro/Login"));
        AssertSinIdentificador(await anonimo.GetStringAsync("/Registro/Signup"));

        var participante = await ParticipanteConEquipoAsync("1508801", "etiqueta.part@uabc.edu.mx", "Equipo Etiqueta");
        AssertSinIdentificador(await participante.GetStringAsync("/Registro/Ficha"));

        // Mensaje de error del portal al agregar a alguien inexistente.
        var agregar = await PostAsync(participante, "/Registro/Ficha", "/Registro/AgregarIntegrante", ("identificador", "1999999"));
        AssertSinIdentificador(await participante.GetStringAsync(agregar.Headers.Location!.OriginalString), conEtiqueta: false);
    }

    [Fact]
    public async Task PantallasDeAdministracion_DicenMatricula()
    {
        await ParticipanteConEquipoAsync("1508802", "etiqueta.admin@uabc.edu.mx", "Equipo Etiqueta Admin");
        await _factory.CrearJuezAsync("admin-etiqueta", Password, rol: "Admin");
        var admin = _factory.CrearCliente();
        await PostAsync(admin, "/Registro/Login", "/Acceso/Login", ("usuario", "admin-etiqueta"), ("password", Password));

        AssertSinIdentificador(await admin.GetStringAsync("/AltarAdmin/RegistrantesYEquipos?tab=registrantes"));

        var registranteId = await ConContextoAsync(c => c.Registrantes.Where(r => r.Identificador == "1508802").Select(r => r.Id).SingleAsync());
        AssertSinIdentificador(await admin.GetStringAsync($"/AltarAdmin/EditarRegistrante/{registranteId}"));

        var equipoId = await ConContextoAsync(c => c.EquiposConcurso.Where(e => e.Nombre == "Equipo Etiqueta Admin").Select(e => e.Id).SingleAsync());
        AssertSinIdentificador(await admin.GetStringAsync($"/AltarAdmin/EditarEquipo/{equipoId}"));
    }

    private async Task<HttpClient> ParticipanteConEquipoAsync(string identificador, string correo, string equipo)
    {
        var participante = _factory.CrearCliente();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(participante, "/Registro/Signup", "/Registro/Signup",
            ("NombreCompleto", "Participante Etiqueta"), ("Identificador", identificador), ("CorreoInstitucional", correo),
            ("Password", Password), ("ConfirmarPassword", Password))).StatusCode);
        var carrera = await CarreraAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(participante, "/Registro/CrearEquipo", "/Registro/CrearEquipo",
            ("Nombre", equipo), ("NombreAltar", "Altar Etiqueta"), ("CarreraId", carrera.ToString()), ("MaestroEncargadoIdentificador", "54321"),
            ("DifuntoNombre", "Difunto Etiqueta"), ("DifuntoFechaDefuncion", "2020-01-15"))).StatusCode);
        return participante;
    }

    private Task<int> CarreraAsync() => ConContextoAsync(c => c.CatalogoCarreras.Where(x => x.Activo).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync());

    private async Task<T> ConContextoAsync<T>(Func<AltarDbContext, Task<T>> accion)
    {
        using var scope = _factory.Services.CreateScope();
        return await accion(scope.ServiceProvider.GetRequiredService<AltarDbContext>());
    }
}
