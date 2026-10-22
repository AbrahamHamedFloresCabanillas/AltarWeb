using System.Net;
using System.Text.RegularExpressions;

namespace Altares.Tests.Integracion;

// Manuales de usuario en PDF (peticion del propietario, 2026-10-22): uno por rol (participante, juez, administrador),
// en una pagina "Manuales" del menu lateral, abajo de "Reporte de Periodo", para jueces y administradores. El del
// participante tambien se enlaza desde su portal.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class ManualesTests
{
    private const string Password = "Manuales-Altares-2026!";
    private readonly AltarWebFactory _factory;
    public ManualesTests(AltarWebFactory factory) => _factory = factory;

    private async Task<HttpClient> JuezAsync(string usuario, string rol)
    {
        await _factory.CrearJuezAsync(usuario, Password, rol: rol);
        var cliente = _factory.CrearCliente();
        var html = await cliente.GetStringAsync("/Registro/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var login = await cliente.PostAsync("/Acceso/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["usuario"] = usuario, ["password"] = Password, ["__RequestVerificationToken"] = token
        }));
        Assert.Contains("Historial", login.Headers.Location!.OriginalString);
        return cliente;
    }

    private static async Task AssertPdfAsync(HttpResponseMessage respuesta, string nombreArchivo)
    {
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Contains(nombreArchivo, respuesta.Content.Headers.ContentDisposition?.ToString());
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 5_000, $"PDF demasiado pequeño ({bytes.Length} bytes)");
    }

    [Fact]
    public async Task Admin_VeElBotonAbajoDeReporte_YDescargaLosTresManuales()
    {
        var admin = await JuezAsync("admin-manuales", "Admin");

        var historial = await admin.GetStringAsync("/AltarEvaluacion/Historial");
        var reporte = historial.IndexOf("asp-action=\"ReportePeriodo\"", StringComparison.Ordinal) is var i and >= 0 ? i : historial.IndexOf("/AltarAdmin/ReportePeriodo", StringComparison.Ordinal);
        var manuales = historial.IndexOf("/Manual\"", StringComparison.Ordinal);
        Assert.True(reporte >= 0 && manuales > reporte, "El boton Manuales debe ir abajo de Reporte de Periodo");

        var pagina = await admin.GetStringAsync("/Manual");
        Assert.Contains("/Manual/Participante", pagina);
        Assert.Contains("/Manual/Juez", pagina);
        Assert.Contains("/Manual/Administrador", pagina);

        await AssertPdfAsync(await admin.GetAsync("/Manual/Participante"), "Manual-Participante-Concurso-de-Altares.pdf");
        await AssertPdfAsync(await admin.GetAsync("/Manual/Juez"), "Manual-Juez-Concurso-de-Altares.pdf");
        await AssertPdfAsync(await admin.GetAsync("/Manual/Administrador"), "Manual-Administrador-Concurso-de-Altares.pdf");
    }

    [Fact]
    public async Task Juez_VeElBoton_PeroNoElManualDeAdministracion()
    {
        var juez = await JuezAsync("juez-manuales", "Juez");

        Assert.Contains("/Manual\"", await juez.GetStringAsync("/AltarEvaluacion/Historial"));
        var pagina = await juez.GetStringAsync("/Manual");
        Assert.DoesNotContain("/Manual/Administrador", pagina);
        await AssertPdfAsync(await juez.GetAsync("/Manual/Juez"), "Manual-Juez");

        var admin = await juez.GetAsync("/Manual/Administrador");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
    }

    [Fact]
    public async Task Anonimo_NoVeLaPaginaNiLosManualesInternos_PeroSiElDelParticipante()
    {
        var anonimo = _factory.CrearCliente();
        Assert.Equal(HttpStatusCode.Redirect, (await anonimo.GetAsync("/Manual")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonimo.GetAsync("/Manual/Juez")).StatusCode);
        await AssertPdfAsync(await anonimo.GetAsync("/Manual/Participante"), "Manual-Participante");
    }

    [Fact]
    public async Task ElPortalDelParticipante_EnlazaSuManual()
    {
        var cliente = _factory.CrearCliente();
        var html = await cliente.GetStringAsync("/Registro/Signup");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        await cliente.PostAsync("/Registro/Signup", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NombreCompleto"] = "Participante Manual", ["Identificador"] = "1508899", ["CorreoInstitucional"] = "participante.manual@uabc.edu.mx",
            ["Password"] = Password, ["ConfirmarPassword"] = Password, ["AceptoAvisoYTerminos"] = "true", ["__RequestVerificationToken"] = token
        }));

        Assert.Contains("/Manual/Participante", await cliente.GetStringAsync("/Registro/Dashboard"));
    }
}

// "Ver" (peticion del propietario, 2026-10-22): el mismo PDF, pero inline para que el navegador lo abra en su visor
// en una pestaña nueva en vez de descargarlo.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class ManualesVerTests
{
    private readonly AltarWebFactory _factory;
    public ManualesVerTests(AltarWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Ver_EntregaElPdfInline_YLosBotonesAbrenPestanaNueva()
    {
        var cliente = _factory.CrearCliente();
        var ver = await cliente.GetAsync("/Manual/Participante?ver=1");
        Assert.Equal("application/pdf", ver.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", ver.Content.Headers.ContentDisposition?.DispositionType);

        var descargar = await cliente.GetAsync("/Manual/Participante");
        Assert.Equal("attachment", descargar.Content.Headers.ContentDisposition?.DispositionType);

        await _factory.CrearJuezAsync("juez-manuales-ver", "Manuales-Ver-2026!");
        var juez = _factory.CrearCliente();
        var token = System.Text.RegularExpressions.Regex.Match(await juez.GetStringAsync("/Registro/Login"), "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        await juez.PostAsync("/Acceso/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["usuario"] = "juez-manuales-ver", ["password"] = "Manuales-Ver-2026!", ["__RequestVerificationToken"] = token }));
        var pagina = await juez.GetStringAsync("/Manual");
        Assert.Contains("href=\"/Manual/Juez?ver=1\" target=\"_blank\"", pagina);
    }
}
