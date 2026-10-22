using System.Net;
using System.Text.RegularExpressions;

namespace Altares.Tests.Integracion;

// M4 (pruebas adversariales 2026-10-21): la pantalla de inicio mostraba cualquier texto recibido en ?error=, asi que
// un enlace como /Registro/Login?error=Tu cuenta fue suspendida. Llama al 555-1234 parecia un aviso oficial. Los
// avisos ahora viajan en TempData, que solo escribe el servidor.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class AvisoLoginTests
{
    private readonly AltarWebFactory _factory;
    public AvisoLoginTests(AltarWebFactory factory) => _factory = factory;

    [Fact]
    public async Task TextoArbitrarioEnLaUrl_NoSeMuestra()
    {
        var html = await _factory.CrearCliente().GetStringAsync("/Registro/Login?error=Tu%20cuenta%20fue%20suspendida.%20Llama%20al%20555-1234");

        Assert.DoesNotContain("555-1234", html);
    }

    [Fact]
    public async Task LoginFallido_MuestraElAvisoDelServidor_SinPonerloEnLaUrl()
    {
        var cliente = _factory.CrearCliente();
        var html = await cliente.GetStringAsync("/Registro/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

        var login = await cliente.PostAsync("/Acceso/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["usuario"] = "no-existe-aviso", ["password"] = "incorrecta", ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var destino = login.Headers.Location!.OriginalString;
        Assert.Matches(@"^/(\?tab=\w+)?$", destino); // pantalla de inicio (ruta por defecto)
        Assert.DoesNotContain("error=", destino);
        Assert.Contains("Usuario o contraseña incorrectos", WebUtility.HtmlDecode(await cliente.GetStringAsync(destino)));
    }
}
