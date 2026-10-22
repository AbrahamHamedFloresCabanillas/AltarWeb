using System.Net;
using Catrinas.Tests.Infraestructura;

namespace Catrinas.Tests;

// M4 (pruebas adversariales 2026-10-21, reportado en Altares; Catrinas tenia el mismo patron): la pantalla de
// inicio mostraba cualquier texto recibido en ?error=. Los avisos ahora viajan en TempData, que solo escribe el
// servidor.
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class AvisoLoginTests
{
    private readonly CatrinasFactory _f;
    public AvisoLoginTests(CatrinasFactory f) => _f = f;

    [Fact]
    public async Task TextoArbitrarioEnLaUrl_NoSeMuestra()
    {
        var html = await _f.Cliente().GetStringAsync("/Registro/Login?error=Tu%20cuenta%20fue%20suspendida.%20Llama%20al%20555-1234");

        Assert.DoesNotContain("555-1234", html);
    }

    [Fact]
    public async Task LoginFallido_MuestraElAvisoDelServidor_SinPonerloEnLaUrl()
    {
        var cliente = _f.Cliente();
        var login = await Http.LoginJuradoAsync(cliente, "no-existe-aviso", "incorrecta");

        Assert.DoesNotContain("error=", login.Headers.Location!.OriginalString);
        Assert.Contains("Usuario o contraseña incorrectos", await Http.AvisoDeLoginAsync(cliente, login));
    }
}
