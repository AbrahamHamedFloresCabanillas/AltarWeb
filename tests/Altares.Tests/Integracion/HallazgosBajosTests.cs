using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using AltarWeb.Models.Registro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Altares.Tests.Integracion;

// Hallazgos bajos de las pruebas adversariales (2026-10-21) en Altares.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class HallazgosBajosTests
{
    private const string Password = "Hallazgos-Bajos-2026!";
    private static int _contador = 6000;
    private readonly AltarWebFactory _factory;
    public HallazgosBajosTests(AltarWebFactory factory) => _factory = factory;

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rutaFormulario, string accion, params (string, string)[] campos)
    {
        var html = await cliente.GetStringAsync(rutaFormulario);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = token;
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }

    private async Task<T> ConContextoAsync<T>(Func<AltarDbContext, Task<T>> accion)
    {
        using var scope = _factory.Services.CreateScope();
        return await accion(scope.ServiceProvider.GetRequiredService<AltarDbContext>());
    }

    private async Task<(HttpClient Cliente, string Correo, int Id)> ParticipanteAsync()
    {
        var n = Interlocked.Increment(ref _contador);
        var correo = $"bajos.{n}@uabc.edu.mx";
        var cliente = _factory.CrearCliente();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(cliente, "/Registro/Signup", "/Registro/Signup",
            ("NombreCompleto", $"Participante Bajos {n}"), ("Identificador", $"14{n:D5}"), ("CorreoInstitucional", correo),
            ("Password", Password), ("ConfirmarPassword", Password))).StatusCode);
        var id = await ConContextoAsync(c => c.Registrantes.Where(r => r.CorreoInstitucional == correo).Select(r => r.Id).SingleAsync());
        return (cliente, correo, id);
    }

    // Un participante desactivado veia "Correo o contraseña incorrectos": el filtro global de Activo hacia que el
    // login no lo encontrara y la rama de "cuenta desactivada" nunca se ejecutaba.
    [Fact]
    public async Task RegistranteDesactivado_ConSuPassword_VeQueSuCuentaFueDesactivada()
    {
        var (_, correo, id) = await ParticipanteAsync();
        await ConContextoAsync(async c => { (await c.Registrantes.SingleAsync(r => r.Id == id)).Activo = false; return await c.SaveChangesAsync(); });

        var respuesta = await PostAsync(_factory.CrearCliente(), "/Registro/Login", "/Registro/Login",
            ("CorreoInstitucional", correo), ("Password", Password));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains("Tu cuenta fue desactivada", html);
        Assert.DoesNotContain("Correo o contraseña incorrectos", html);
    }

    [Fact]
    public async Task RegistranteDesactivado_ConPasswordIncorrecta_SigueViendoElMensajeGenerico()
    {
        var (_, correo, id) = await ParticipanteAsync();
        await ConContextoAsync(async c => { (await c.Registrantes.SingleAsync(r => r.Id == id)).Activo = false; return await c.SaveChangesAsync(); });

        var respuesta = await PostAsync(_factory.CrearCliente(), "/Registro/Login", "/Registro/Login",
            ("CorreoInstitucional", correo), ("Password", "otra-cosa"));

        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains("Correo o contraseña incorrectos", html);
        Assert.DoesNotContain("desactivada", html);
    }

    // Con una peticion alterada, designar Narrador a alguien que no es del equipo quitaba en silencio al actual.
    [Fact]
    public async Task DesignarNarrador_QueNoEsIntegrante_NoQuitaAlActual()
    {
        var (organizador, _, id) = await ParticipanteAsync();
        var carrera = await ConContextoAsync(c => c.CatalogoCarreras.Where(x => x.Activo).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(organizador, "/Registro/CrearEquipo", "/Registro/CrearEquipo",
            ("Nombre", $"Equipo Bajos {id}"), ("NombreAltar", "Altar Bajos"), ("CarreraId", carrera.ToString()), ("MaestroEncargadoIdentificador", "54321"),
            ("DifuntoNombre", "Difunto Bajos"), ("DifuntoFechaDefuncion", "2020-01-15"))).StatusCode);
        await PostAsync(organizador, "/Registro/Ficha", "/Registro/DesignarNarrador", ("registranteId", id.ToString()));
        Assert.True(await ConContextoAsync(c => c.RegistranteEquipos.AnyAsync(x => x.RegistranteId == id && x.Rol == RolEquipo.Narrador)));

        var respuesta = await PostAsync(organizador, "/Registro/Ficha", "/Registro/DesignarNarrador", ("registranteId", "999999"));

        Assert.True(await ConContextoAsync(c => c.RegistranteEquipos.AnyAsync(x => x.RegistranteId == id && x.Rol == RolEquipo.Narrador)),
            "El Narrador actual se quito en silencio");
        Assert.Contains("no es integrante", WebUtility.HtmlDecode(await organizador.GetStringAsync(respuesta.Headers.Location!.OriginalString)));
    }
}
