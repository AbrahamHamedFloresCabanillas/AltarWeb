using System.Net;
using System.Text.RegularExpressions;
using Catrinas.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Tests;

// Avisos legales (peticion del propietario, 2026-10-22): aviso de privacidad integral, aviso simplificado al recabar
// datos, terminos de uso, enlaces desde el inicio de sesion, aceptacion obligatoria al crear cuenta (con fecha y
// version) y consentimiento expreso aparte para los datos sensibles (genero y autodescripcion cultural).
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class AvisosLegalesTests
{
    private readonly CatrinasFactory _f;
    public AvisosLegalesTests(CatrinasFactory f) => _f = f;

    private static string Texto(string html) => WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " "));

    [Fact]
    public async Task ElInicioDeSesion_EnlazaElAvisoDePrivacidadYLosTerminos()
    {
        var login = await _f.Cliente().GetStringAsync("/Registro/Login");
        Assert.Contains("href=\"/Legal/Privacidad\"", login);
        Assert.Contains("href=\"/Legal/Terminos\"", login);
    }

    [Fact]
    public async Task ElAvisoDePrivacidad_EsPublico_YTieneLosElementosRequeridos()
    {
        var respuesta = await _f.Cliente().GetAsync("/Legal/Privacidad");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var texto = Texto(await respuesta.Content.ReadAsStringAsync());

        Assert.Contains("Aviso de privacidad integral", texto);
        Assert.Contains("Concurso de Catrinas", texto);
        Assert.Contains("datos personales sensibles", texto);
        Assert.Contains("Estados Unidos", texto);
        Assert.Contains("ARCO", texto);
        Assert.Contains("[POR DEFINIR]", texto);
    }

    [Fact]
    public async Task LosTerminos_SonPublicos()
    {
        var respuesta = await _f.Cliente().GetAsync("/Legal/Terminos");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Términos de uso", Texto(await respuesta.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task ElRegistro_MuestraElAvisoSimplificado()
    {
        Assert.Contains("Aviso de privacidad simplificado", Texto(await _f.Cliente().GetStringAsync("/Registro/Signup")));
        Assert.Contains("Aviso de privacidad simplificado", Texto(await _f.Cliente().GetStringAsync("/Registro/SolicitarJurado")));
    }

    private Catrinas.Web.Modelos.Registrante? Registrante(string correo) =>
        _f.ConContexto(c => c.Registrantes.AsNoTracking().FirstOrDefault(r => r.CorreoInstitucional == correo));

    [Fact]
    public async Task SinAceptarElAviso_NoSeCreaLaCuenta()
    {
        var n = Escenario.Siguiente();
        var correo = $"avisos.cat{n}@uabc.edu.mx";
        var respuesta = await Http.SignupAsync(_f.Cliente(), "Persona Avisos", $"10{n:D5}", correo, acepta: false);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Null(Registrante(correo));
    }

    [Fact]
    public async Task AlAceptar_SeGuardaLaFechaYLaVersion()
    {
        var n = Escenario.Siguiente();
        var correo = $"avisos.cat{n}@uabc.edu.mx";
        Http.Redirige(await Http.SignupAsync(_f.Cliente(), "Persona Avisos", $"10{n:D5}", correo), "Panel");

        var r = Registrante(correo)!;
        Assert.NotNull(r.AvisoPrivacidadAceptadoEn);
        Assert.Equal(Catrinas.Web.AvisosLegales.Version, r.AvisoPrivacidadVersion);
        Assert.Null(r.ConsentimientoSensiblesEn);
    }

    [Fact]
    public async Task DatosSensibles_SinConsentimientoExpreso_SeRechazan()
    {
        var n = Escenario.Siguiente();
        var correo = $"avisos.cat{n}@uabc.edu.mx";
        var respuesta = await Http.SignupAsync(_f.Cliente(), "Persona Avisos", $"10{n:D5}", correo, extra: ("AutodescripcionCultural", "Pueblo originario"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("consentimiento expreso", Texto(await respuesta.Content.ReadAsStringAsync()));
        Assert.Null(Registrante(correo));
    }

    [Fact]
    public async Task DatosSensibles_ConConsentimientoExpreso_SeGuardanConSuFecha()
    {
        var n = Escenario.Siguiente();
        var correo = $"avisos.cat{n}@uabc.edu.mx";
        Http.Redirige(await Http.SignupAsync(_f.Cliente(), "Persona Avisos", $"10{n:D5}", correo,
            extra: new[] { ("AutodescripcionCultural", "Pueblo originario"), ("ConsentimientoDatosSensibles", "true") }), "Panel");

        var r = Registrante(correo)!;
        Assert.NotNull(r.ConsentimientoSensiblesEn);
        Assert.Equal("Pueblo originario", r.AutodescripcionCultural);
    }

    [Fact]
    public async Task LaSolicitudDeJurado_TambienExigeAceptarElAviso()
    {
        var n = Escenario.Siguiente();
        var correo = $"jurado.avisos{n}@uabc.edu.mx";
        var campos = new List<(string, string)>
        {
            ("NombreCompleto", "Jurado Avisos"), ("CorreoInstitucional", correo), ("Identificador", "54321"),
            ("Password", "Jurado-Avisos-2026!"), ("ConfirmarPassword", "Jurado-Avisos-2026!")
        };
        Assert.Equal(HttpStatusCode.OK, (await Http.PostAsync(_f.Cliente(), "/Registro/SolicitarJurado", campos.ToArray())).StatusCode);

        campos.Add(("AceptoAvisoYTerminos", "true"));
        Http.Redirige(await Http.PostAsync(_f.Cliente(), "/Registro/SolicitarJurado", campos.ToArray()), "jurado");
        Assert.Equal(Catrinas.Web.AvisosLegales.Version,
            _f.ConContexto(c => c.Jueces.Single(j => j.CorreoInstitucional == correo).AvisoPrivacidadVersion));
    }
}
