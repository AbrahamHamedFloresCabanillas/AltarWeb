using System.Net;
using System.Text.RegularExpressions;
using Catrinas.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Tests;

// A1 (pruebas adversariales 2026-10-21): el responsable agregaba a cualquier persona escribiendo su matricula, sin su
// consentimiento (la confirmacion solo era un confirm() del navegador), y la persona quedaba atada a la inscripcion
// todo el periodo. Decision del propietario: invitacion que la persona acepta. El mensaje al responsable es siempre
// el mismo.
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class InvitacionesInscripcionTests
{
    private const string MensajeGenerico = "verá tu invitación en su panel";
    private readonly CatrinasFactory _f;
    public InvitacionesInscripcionTests(CatrinasFactory f) => _f = f;

    private sealed record Persona(HttpClient Cliente, int Id, string Matricula, string Nombre);

    private async Task<Persona> PersonaAsync()
    {
        var n = Escenario.Siguiente();
        var matricula = $"11{n:D5}";
        var nombre = $"Persona Invitada {n}";
        var cliente = _f.Cliente();
        Http.Redirige(await Http.SignupAsync(cliente, nombre, matricula, $"invitada.cat{n}@uabc.edu.mx"), "Panel");
        return new Persona(cliente, _f.ConContexto(c => c.Registrantes.Single(r => r.Identificador == matricula).Id), matricula, nombre);
    }

    private async Task<(Persona Responsable, int InscripcionId)> InscripcionAsync()
    {
        var responsable = await PersonaAsync();
        var nombre = $"Catrina de {responsable.Nombre}";
        Http.Redirige(await Http.CrearInscripcionAsync(responsable.Cliente, nombre, 11), "Inscripcion/Editar");
        return (responsable, _f.ConContexto(c => c.Inscripciones.Single(i => i.Nombre == nombre).Id));
    }

    private Task<HttpResponseMessage> InvitarAsync(Persona responsable, string matricula) =>
        Http.PostDesdeAsync(responsable.Cliente, "/Inscripcion/Editar", "/Inscripcion/AgregarIntegrante", ("identificador", matricula));

    private static async Task<string> SeguirAsync(HttpClient cliente, HttpResponseMessage r) =>
        WebUtility.HtmlDecode(await cliente.GetStringAsync(r.Headers.Location!.OriginalString));

    private bool EsMiembro(Persona persona, int inscripcionId) =>
        _f.ConContexto(c => c.Miembros.Any(m => m.RegistranteId == persona.Id && m.InscripcionId == inscripcionId));

    private int InvitacionDe(Persona persona) => _f.ConContexto(c => c.Invitaciones.Single(i => i.RegistranteId == persona.Id).Id);

    [Fact]
    public async Task Invitar_NoAgregaHastaQueLaPersonaAcepta()
    {
        var (responsable, inscripcionId) = await InscripcionAsync();
        var invitada = await PersonaAsync();

        var mensaje = await SeguirAsync(responsable.Cliente, await InvitarAsync(responsable, invitada.Matricula));

        Assert.Contains(MensajeGenerico, mensaje);
        Assert.DoesNotContain(invitada.Nombre, mensaje);
        Assert.False(EsMiembro(invitada, inscripcionId));

        var panel = WebUtility.HtmlDecode(await invitada.Cliente.GetStringAsync("/Registro/Panel"));
        Assert.Contains($"Catrina de {responsable.Nombre}", panel);
        var invitacionId = Regex.Match(panel, "name=\"invitacionId\" value=\"(\\d+)\"").Groups[1].Value;
        Assert.NotEmpty(invitacionId);

        Http.Redirige(await Http.PostDesdeAsync(invitada.Cliente, "/Registro/Panel", "/Inscripcion/AceptarInvitacion", ("invitacionId", invitacionId)), "Panel");

        Assert.True(EsMiembro(invitada, inscripcionId));
        Assert.False(_f.ConContexto(c => c.Invitaciones.Any(i => i.RegistranteId == invitada.Id)));
    }

    [Fact]
    public async Task Rechazar_NoAgrega_YLaPersonaPuedeCrearSuPropiaInscripcion()
    {
        var (responsable, inscripcionId) = await InscripcionAsync();
        var invitada = await PersonaAsync();
        await InvitarAsync(responsable, invitada.Matricula);

        await Http.PostDesdeAsync(invitada.Cliente, "/Registro/Panel", "/Inscripcion/RechazarInvitacion", ("invitacionId", InvitacionDe(invitada).ToString()));

        Assert.False(EsMiembro(invitada, inscripcionId));
        Assert.False(_f.ConContexto(c => c.Invitaciones.Any(i => i.RegistranteId == invitada.Id)));
        Http.Redirige(await Http.CrearInscripcionAsync(invitada.Cliente, $"Propia de {invitada.Nombre}", 11), "Inscripcion/Editar");
    }

    [Fact]
    public async Task ElMensajeAlResponsable_NoRevelaSiLaPersonaExisteOTieneInscripcion()
    {
        var (responsable, _) = await InscripcionAsync();
        var (otra, _) = await InscripcionAsync();

        var aInexistente = await SeguirAsync(responsable.Cliente, await InvitarAsync(responsable, "1999997"));
        var aConInscripcion = await SeguirAsync(responsable.Cliente, await InvitarAsync(responsable, otra.Matricula));

        Assert.Contains(MensajeGenerico, aInexistente);
        Assert.Contains(MensajeGenerico, aConInscripcion);
        Assert.DoesNotContain(otra.Nombre, aConInscripcion);
    }

    [Fact]
    public async Task SoloLaInvitadaPuedeAceptarSuInvitacion()
    {
        var (responsable, inscripcionId) = await InscripcionAsync();
        var invitada = await PersonaAsync();
        var intrusa = await PersonaAsync();
        await InvitarAsync(responsable, invitada.Matricula);

        // Su panel no tiene formularios (no tiene invitaciones): el token sale de "Crear inscripcion".
        await Http.PostDesdeAsync(intrusa.Cliente, "/Inscripcion/Crear", "/Inscripcion/AceptarInvitacion", ("invitacionId", InvitacionDe(invitada).ToString()));

        Assert.False(EsMiembro(intrusa, inscripcionId));
        Assert.False(EsMiembro(invitada, inscripcionId));
    }

    [Fact]
    public async Task ElResponsablePuedeCancelarUnaInvitacionPendiente()
    {
        var (responsable, _) = await InscripcionAsync();
        var invitada = await PersonaAsync();
        await InvitarAsync(responsable, invitada.Matricula);
        Assert.Contains(invitada.Matricula, await responsable.Cliente.GetStringAsync("/Inscripcion/Editar"));

        await Http.PostDesdeAsync(responsable.Cliente, "/Inscripcion/Editar", "/Inscripcion/CancelarInvitacion", ("invitacionId", InvitacionDe(invitada).ToString()));

        Assert.False(_f.ConContexto(c => c.Invitaciones.Any(i => i.RegistranteId == invitada.Id)));
    }

    [Fact]
    public async Task EditarYaNoPideConfirmacionDelNavegador()
    {
        var (responsable, _) = await InscripcionAsync();
        Assert.DoesNotContain("confirm(", await responsable.Cliente.GetStringAsync("/Inscripcion/Editar"));
    }
}
