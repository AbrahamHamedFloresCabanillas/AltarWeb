using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Altares.Tests.Integracion;

// A1 (pruebas adversariales 2026-10-21): el organizador agregaba a cualquier persona escribiendo su matricula, sin su
// consentimiento; la persona no podia salir ni crear su propio equipo, y el mensaje revelaba su nombre y si ya tenia
// equipo. Decision del propietario: invitacion que la persona acepta. El mensaje al organizador es siempre el mismo.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class InvitacionesEquipoTests
{
    private const string Password = "Invitaciones-Equipo-2026!";
    private const string MensajeGenerico = "verá tu invitación en su portal";
    private static int _contador = 7000;
    private readonly AltarWebFactory _factory;
    public InvitacionesEquipoTests(AltarWebFactory factory) => _factory = factory;

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rutaFormulario, string accion, params (string, string)[] campos)
    {
        var html = await cliente.GetStringAsync(rutaFormulario);
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }

    private static async Task<string> SeguirAsync(HttpClient cliente, HttpResponseMessage r) =>
        WebUtility.HtmlDecode(await cliente.GetStringAsync(r.Headers.Location!.OriginalString));

    private async Task<T> ConContextoAsync<T>(Func<AltarDbContext, Task<T>> accion)
    {
        using var scope = _factory.Services.CreateScope();
        return await accion(scope.ServiceProvider.GetRequiredService<AltarDbContext>());
    }

    private sealed record Persona(HttpClient Cliente, int Id, string Matricula, string Nombre);

    private async Task<Persona> PersonaAsync()
    {
        var n = Interlocked.Increment(ref _contador);
        var matricula = $"12{n:D5}";
        var nombre = $"Persona Invitaciones {n}";
        var cliente = _factory.CrearCliente();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(cliente, "/Registro/Signup", "/Registro/Signup",
            ("NombreCompleto", nombre), ("Identificador", matricula), ("CorreoInstitucional", $"invitaciones.{n}@uabc.edu.mx"),
            ("Password", Password), ("ConfirmarPassword", Password), ("AceptoAvisoYTerminos", "true"))).StatusCode);
        var id = await ConContextoAsync(c => c.Registrantes.Where(r => r.Identificador == matricula).Select(r => r.Id).SingleAsync());
        return new Persona(cliente, id, matricula, nombre);
    }

    private async Task<int> CrearEquipoAsync(Persona organizador)
    {
        var carrera = await ConContextoAsync(c => c.CatalogoCarreras.Where(x => x.Activo).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(organizador.Cliente, "/Registro/CrearEquipo", "/Registro/CrearEquipo",
            ("Nombre", $"Equipo de {organizador.Nombre}"), ("NombreAltar", "Altar invitaciones"), ("CarreraId", carrera.ToString()),
            ("MaestroEncargadoIdentificador", "54321"), ("DifuntoNombre", "Difunto invitaciones"), ("DifuntoFechaDefuncion", "2020-01-15"))).StatusCode);
        return await ConContextoAsync(c => c.EquiposConcurso.Where(e => e.CreadoPorRegistranteId == organizador.Id).Select(e => e.Id).SingleAsync());
    }

    private Task<bool> EsIntegranteAsync(Persona persona, int equipoId) =>
        ConContextoAsync(c => c.RegistranteEquipos.AnyAsync(re => re.RegistranteId == persona.Id && re.EquipoId == equipoId));

    private Task<HttpResponseMessage> InvitarAsync(Persona organizador, string matricula) =>
        PostAsync(organizador.Cliente, "/Registro/Ficha", "/Registro/AgregarIntegrante", ("identificador", matricula));

    [Fact]
    public async Task Invitar_NoAgregaHastaQueLaPersonaAcepta()
    {
        var organizador = await PersonaAsync();
        var invitado = await PersonaAsync();
        var equipoId = await CrearEquipoAsync(organizador);

        var mensaje = await SeguirAsync(organizador.Cliente, await InvitarAsync(organizador, invitado.Matricula));

        Assert.Contains(MensajeGenerico, mensaje);
        Assert.DoesNotContain(invitado.Nombre, mensaje);
        Assert.False(await EsIntegranteAsync(invitado, equipoId));

        var portal = WebUtility.HtmlDecode(await invitado.Cliente.GetStringAsync("/Registro/Dashboard"));
        Assert.Contains($"Equipo de {organizador.Nombre}", portal);
        var invitacionId = Regex.Match(portal, "name=\"invitacionId\" value=\"(\\d+)\"").Groups[1].Value;
        Assert.NotEmpty(invitacionId);

        await PostAsync(invitado.Cliente, "/Registro/Dashboard", "/Registro/AceptarInvitacion", ("invitacionId", invitacionId));

        Assert.True(await EsIntegranteAsync(invitado, equipoId));
        Assert.False(await ConContextoAsync(c => c.InvitacionesEquipo.AnyAsync(i => i.RegistranteId == invitado.Id)));
    }

    [Fact]
    public async Task Rechazar_NoAgrega_YLaPersonaPuedeCrearSuPropioEquipo()
    {
        var organizador = await PersonaAsync();
        var invitado = await PersonaAsync();
        var equipoId = await CrearEquipoAsync(organizador);
        await InvitarAsync(organizador, invitado.Matricula);
        var invitacionId = await ConContextoAsync(c => c.InvitacionesEquipo.Where(i => i.RegistranteId == invitado.Id).Select(i => i.Id).SingleAsync());

        await PostAsync(invitado.Cliente, "/Registro/Dashboard", "/Registro/RechazarInvitacion", ("invitacionId", invitacionId.ToString()));

        Assert.False(await EsIntegranteAsync(invitado, equipoId));
        Assert.False(await ConContextoAsync(c => c.InvitacionesEquipo.AnyAsync(i => i.Id == invitacionId)));
        await CrearEquipoAsync(invitado);
    }

    [Fact]
    public async Task ElMensajeAlOrganizador_NoRevelaSiLaPersonaExisteOTieneEquipo()
    {
        var organizador = await PersonaAsync();
        await CrearEquipoAsync(organizador);
        var conEquipo = await PersonaAsync();
        await CrearEquipoAsync(conEquipo);

        var aInexistente = await SeguirAsync(organizador.Cliente, await InvitarAsync(organizador, "1999998"));
        var aConEquipo = await SeguirAsync(organizador.Cliente, await InvitarAsync(organizador, conEquipo.Matricula));

        Assert.Contains(MensajeGenerico, aInexistente);
        Assert.Contains(MensajeGenerico, aConEquipo);
        Assert.DoesNotContain(conEquipo.Nombre, aConEquipo);
        Assert.DoesNotContain("ya pertenece", aConEquipo);
    }

    [Fact]
    public async Task SoloElInvitadoPuedeAceptarSuInvitacion()
    {
        var organizador = await PersonaAsync();
        var invitado = await PersonaAsync();
        var intruso = await PersonaAsync();
        var equipoId = await CrearEquipoAsync(organizador);
        await InvitarAsync(organizador, invitado.Matricula);
        var invitacionId = await ConContextoAsync(c => c.InvitacionesEquipo.Where(i => i.RegistranteId == invitado.Id).Select(i => i.Id).SingleAsync());

        await PostAsync(intruso.Cliente, "/Registro/Dashboard", "/Registro/AceptarInvitacion", ("invitacionId", invitacionId.ToString()));

        Assert.False(await EsIntegranteAsync(intruso, equipoId));
        Assert.False(await EsIntegranteAsync(invitado, equipoId));
    }

    [Fact]
    public async Task ElOrganizadorPuedeCancelarUnaInvitacionPendiente()
    {
        var organizador = await PersonaAsync();
        var invitado = await PersonaAsync();
        await CrearEquipoAsync(organizador);
        await InvitarAsync(organizador, invitado.Matricula);
        Assert.Contains(invitado.Matricula, await organizador.Cliente.GetStringAsync("/Registro/Ficha"));
        var invitacionId = await ConContextoAsync(c => c.InvitacionesEquipo.Where(i => i.RegistranteId == invitado.Id).Select(i => i.Id).SingleAsync());

        await PostAsync(organizador.Cliente, "/Registro/Ficha", "/Registro/CancelarInvitacion", ("invitacionId", invitacionId.ToString()));

        Assert.False(await ConContextoAsync(c => c.InvitacionesEquipo.AnyAsync(i => i.Id == invitacionId)));
    }
}
