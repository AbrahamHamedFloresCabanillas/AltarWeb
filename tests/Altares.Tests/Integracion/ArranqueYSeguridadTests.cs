using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace Altares.Tests.Integracion;

[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class ArranqueYSeguridadTests
{
    private readonly AltarWebFactory _factory;

    public ArranqueYSeguridadTests(AltarWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Landing_Responde200ConCabecerasDeSeguridad()
    {
        var respuesta = await _factory.CrearCliente().GetAsync("/Registro/Login");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'self'", respuesta.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(respuesta.Headers.Contains("Strict-Transport-Security"));
    }

    // SEC-08: la autorizacion es manual en cada accion; esta lista detecta si una ruta protegida
    // queda accesible sin sesion (p. ej. al mover controladores durante la separacion).
    [Theory]
    [InlineData("/AltarEvaluacion/Historial")]
    [InlineData("/AltarEvaluacion/NuevaEvaluacion")]
    [InlineData("/AltarEvaluacion/Detalle/1")]
    [InlineData("/AltarEvaluacion/RecorridoPdf")]
    [InlineData("/AltarAdmin/Jueces")]
    [InlineData("/AltarAdmin/CrearJuez")]
    [InlineData("/AltarAdmin/RegistrantesYEquipos")]
    [InlineData("/AltarAdmin/EditarEquipo/1")]
    [InlineData("/AltarAdmin/Configuracion")]
    [InlineData("/AltarAdmin/ReportePeriodo")]
    [InlineData("/AltarAdmin/ReportePeriodoPdf")]
    [InlineData("/AltarAdmin/Diagnostico")]
    [InlineData("/Constancia/DescargarGrupal/1")]
    [InlineData("/Constancia/DescargarIndividuales/1")]
    [InlineData("/Constancia/DescargarJuez/1")]
    [InlineData("/Constancia/DescargarMia")]
    [InlineData("/Registro/Dashboard")]
    [InlineData("/Registro/Ficha")]
    public async Task RutaProtegida_SinSesion_Redirige(string ruta)
    {
        var respuesta = await _factory.CrearCliente().GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        // Login es la accion por defecto de la ruta, asi que RedirectToAction("Login", "Acceso")
        // genera "/Acceso" y RedirectToAction("Login", "Registro") genera "/".
        var destino = respuesta.Headers.Location!.OriginalString.Split('?')[0];
        Assert.Contains(destino, new[] { "/", "/Acceso", "/Registro/Login", "/Acceso/Login" });
    }

    // SEC-10: el antiforgery global rechaza un POST sin token.
    [Fact]
    public async Task Post_SinTokenAntiforgery_Responde400()
    {
        var respuesta = await _factory.CrearCliente().PostAsync("/Registro/CompletarGoogle",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Identificador"] = "90007" }));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // SEC-01: con token antiforgery valido pero SIN haber pasado por Google, CompletarGoogle no crea
    // cuenta ni sesion: la identidad solo se acepta desde la fuente server-side (IdentidadGoogleVerificada).
    [Fact]
    public async Task CompletarGoogle_SinIdentidadVerificada_NoCreaCuenta()
    {
        var cliente = _factory.CrearCliente();
        var token = await ObtenerTokenAsync(cliente, "/Registro/Login");

        var respuesta = await cliente.PostAsync("/Registro/CompletarGoogle", Formulario(token,
            ("NombreCompleto", "Intruso"), ("CorreoInstitucional", "intruso@uabc.edu.mx"),
            ("Identificador", "90007"), ("Tipo", "Maestro")));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Contains("error=", respuesta.Headers.Location!.ToString());
        var dashboard = await cliente.GetAsync("/Registro/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarWeb.Models.AltarDbContext>();
        Assert.False(context.Registrantes.Any(r => r.CorreoInstitucional == "intruso@uabc.edu.mx"));
    }

    // Google OAuth: el challenge sale hacia Google con el callback HTTPS esperado (CFG-02).
    [Fact]
    public async Task GoogleLogin_RedirigeAGoogleConCallbackHttps()
    {
        var respuesta = await _factory.CrearCliente().GetAsync("/Registro/GoogleLogin?intent=registrante");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var destino = respuesta.Headers.Location!.ToString();
        Assert.StartsWith("https://accounts.google.com/", destino);
        Assert.Contains(Uri.EscapeDataString(AltarWebFactory.Host + "/Registro/google-callback"), destino);
    }

    // SEC-03 + SEC-05 + sesion: el admin semilla entra, la cookie de sesion es Secure/HttpOnly, el
    // cambio de contrasena es obligatorio antes de cualquier otra accion y despues el panel abre.
    [Fact]
    public async Task AdminSemilla_LoginConCambioObligatorio_YSesionFunciona()
    {
        var cliente = _factory.CrearCliente();
        var token = await ObtenerTokenAsync(cliente, "/Registro/Login");

        var login = await cliente.PostAsync("/Acceso/Login", Formulario(token,
            ("usuario", AltarWebFactory.AdminUsuario), ("password", AltarWebFactory.AdminPassword)));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.EndsWith("/Acceso/CambiarPasswordObligatorio", login.Headers.Location!.ToString());

        var cookieSesion = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Session"));
        Assert.Contains("secure", cookieSesion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookieSesion, StringComparison.OrdinalIgnoreCase);

        var bloqueado = await cliente.GetAsync("/AltarAdmin/Jueces");
        Assert.Equal(HttpStatusCode.Redirect, bloqueado.StatusCode);
        Assert.Contains("CambiarPasswordObligatorio", bloqueado.Headers.Location!.ToString());

        token = await ObtenerTokenAsync(cliente, "/Acceso/CambiarPasswordObligatorio");
        var nueva = "Nueva-Pruebas-2026!";
        var cambio = await cliente.PostAsync("/Acceso/CambiarPasswordObligatorio", Formulario(token,
            ("nuevaPassword", nueva), ("confirmarPassword", nueva)));
        Assert.Equal(HttpStatusCode.Redirect, cambio.StatusCode);

        var panel = await cliente.GetAsync("/AltarAdmin/Jueces");
        Assert.Equal(HttpStatusCode.OK, panel.StatusCode);
    }

    [Fact]
    public async Task Login_ConPasswordIncorrecta_NoCreaSesion()
    {
        var cliente = _factory.CrearCliente();
        var token = await ObtenerTokenAsync(cliente, "/Registro/Login");

        var login = await cliente.PostAsync("/Acceso/Login", Formulario(token,
            ("usuario", AltarWebFactory.AdminUsuario), ("password", "incorrecta")));

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("error=", login.Headers.Location!.ToString());
        var panel = await cliente.GetAsync("/AltarEvaluacion/Historial");
        Assert.Equal(HttpStatusCode.Redirect, panel.StatusCode);
    }

    private static async Task<string> ObtenerTokenAsync(HttpClient cliente, string ruta)
    {
        var html = await cliente.GetStringAsync(ruta);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success, $"No se encontro el token antiforgery en {ruta}");
        return match.Groups[1].Value;
    }

    private static FormUrlEncodedContent Formulario(string token, params (string Clave, string Valor)[] campos)
    {
        var datos = campos.ToDictionary(c => c.Clave, c => c.Valor);
        datos["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(datos);
    }
}
