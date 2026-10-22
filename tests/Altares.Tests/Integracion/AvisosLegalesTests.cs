using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Altares.Tests.Integracion;

// Avisos legales (peticion del propietario, 2026-10-22): aviso de privacidad integral, aviso simplificado al recabar
// datos, terminos de uso, enlaces desde el inicio de sesion, aceptacion obligatoria al crear cuenta (con fecha y
// version) y consentimiento expreso aparte para los datos sensibles (genero y autodescripcion cultural).
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class AvisosLegalesTests
{
    private const string Password = "Avisos-Legales-2026!";
    private static int _contador = 8000;
    private readonly AltarWebFactory _factory;
    public AvisosLegalesTests(AltarWebFactory factory) => _factory = factory;

    private static string Texto(string html) => WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " "));

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rutaFormulario, string accion, IEnumerable<(string, string)> campos)
    {
        var html = await cliente.GetStringAsync(rutaFormulario);
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }

    [Fact]
    public async Task ElInicioDeSesion_EnlazaElAvisoDePrivacidadYLosTerminos()
    {
        var login = await _factory.CrearCliente().GetStringAsync("/Registro/Login");
        Assert.Contains("href=\"/Legal/Privacidad\"", login);
        Assert.Contains("href=\"/Legal/Terminos\"", login);
    }

    [Fact]
    public async Task ElAvisoDePrivacidad_EsPublico_YTieneLosElementosRequeridos()
    {
        var respuesta = await _factory.CrearCliente().GetAsync("/Legal/Privacidad");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var texto = Texto(await respuesta.Content.ReadAsStringAsync());

        Assert.Contains("Aviso de privacidad integral", texto);
        Assert.Contains("Facultad de Ingeniería", texto);
        Assert.Contains("datos personales sensibles", texto);
        Assert.Contains("Estados Unidos", texto);
        Assert.Contains("ARCO", texto);
        Assert.Contains("[POR DEFINIR]", texto); // correo y domicilio aun no configurados en pruebas
    }

    [Fact]
    public async Task LosTerminos_SonPublicos()
    {
        var respuesta = await _factory.CrearCliente().GetAsync("/Legal/Terminos");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Términos de uso", Texto(await respuesta.Content.ReadAsStringAsync()));
    }

    private (int N, List<(string, string)> Campos) Signup(params (string, string)[] extra)
    {
        var n = Interlocked.Increment(ref _contador);
        var campos = new List<(string, string)>
        {
            ("NombreCompleto", $"Persona Avisos {n}"), ("Identificador", $"10{n:D5}"), ("CorreoInstitucional", $"avisos.{n}@uabc.edu.mx"),
            ("Password", Password), ("ConfirmarPassword", Password)
        };
        campos.AddRange(extra);
        return (n, campos);
    }

    private AltarWeb.Models.Registro.Registrante? Registrante(int n)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AltarDbContext>().Registrantes.AsNoTracking()
            .FirstOrDefault(r => r.CorreoInstitucional == $"avisos.{n}@uabc.edu.mx");
    }

    [Fact]
    public async Task ElRegistro_MuestraElAvisoSimplificado()
    {
        var texto = Texto(await _factory.CrearCliente().GetStringAsync("/Registro/Signup"));
        Assert.Contains("Aviso de privacidad simplificado", texto);
    }

    [Fact]
    public async Task SinAceptarElAviso_NoSeCreaLaCuenta()
    {
        var (n, campos) = Signup();
        var respuesta = await PostAsync(_factory.CrearCliente(), "/Registro/Signup", "/Registro/Signup", campos);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Null(Registrante(n));
    }

    [Fact]
    public async Task AlAceptar_SeGuardaLaFechaYLaVersion()
    {
        var (n, campos) = Signup(("AceptoAvisoYTerminos", "true"));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(_factory.CrearCliente(), "/Registro/Signup", "/Registro/Signup", campos)).StatusCode);

        var r = Registrante(n)!;
        Assert.NotNull(r.AvisoPrivacidadAceptadoEn);
        Assert.Equal(AltarWeb.Services.AvisosLegales.Version, r.AvisoPrivacidadVersion);
        Assert.Null(r.ConsentimientoSensiblesEn);
    }

    [Fact]
    public async Task DatosSensibles_SinConsentimientoExpreso_SeRechazan()
    {
        var (n, campos) = Signup(("AceptoAvisoYTerminos", "true"), ("AutodescripcionCultural", "Pueblo originario"));
        var respuesta = await PostAsync(_factory.CrearCliente(), "/Registro/Signup", "/Registro/Signup", campos);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("consentimiento expreso", Texto(await respuesta.Content.ReadAsStringAsync()));
        Assert.Null(Registrante(n));
    }

    [Fact]
    public async Task DatosSensibles_ConConsentimientoExpreso_SeGuardanConSuFecha()
    {
        var genero = await Task.Run(() =>
        {
            using var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<AltarDbContext>().CatalogoGeneros.Where(g => g.Activo).Select(g => g.Id).First();
        });
        var (n, campos) = Signup(("AceptoAvisoYTerminos", "true"), ("ConsentimientoDatosSensibles", "true"),
            ("CatalogoGeneroId", genero.ToString()), ("AutodescripcionCultural", "Pueblo originario"));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(_factory.CrearCliente(), "/Registro/Signup", "/Registro/Signup", campos)).StatusCode);

        var r = Registrante(n)!;
        Assert.NotNull(r.ConsentimientoSensiblesEn);
        Assert.Equal("Pueblo originario", r.AutodescripcionCultural);
    }

    [Fact]
    public async Task LaSolicitudDeJuez_TambienExigeAceptarElAviso()
    {
        var n = Interlocked.Increment(ref _contador);
        var campos = new List<(string, string)>
        {
            ("NombreCompleto", "Juez Avisos"), ("CorreoInstitucional", $"juez.avisos.{n}@uabc.edu.mx"), ("Identificador", $"6{n % 10000:D4}"),
            ("Password", Password), ("ConfirmarPassword", Password)
        };
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(_factory.CrearCliente(), "/Registro/SignupJuez", "/Registro/SignupJuez", campos)).StatusCode);

        campos.Add(("AceptoAvisoYTerminos", "true"));
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(_factory.CrearCliente(), "/Registro/SignupJuez", "/Registro/SignupJuez", campos)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var juez = await scope.ServiceProvider.GetRequiredService<AltarDbContext>().Jueces.IgnoreQueryFilters()
            .SingleAsync(j => j.CorreoInstitucional == $"juez.avisos.{n}@uabc.edu.mx");
        Assert.Equal(AltarWeb.Services.AvisosLegales.Version, juez.AvisoPrivacidadVersion);
    }
}
