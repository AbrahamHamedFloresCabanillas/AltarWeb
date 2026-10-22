using System.Text.RegularExpressions;

namespace Altares.Tests.Integracion;

// Peticion del propietario (2026-10-22): la marca de arriba a la izquierda dice "Concurso de Altares" (antes "AltarWeb"),
// igual que Catrinas dice "Concurso de Catrinas".
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class MarcaTests
{
    private readonly AltarWebFactory _factory;
    public MarcaTests(AltarWebFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/Registro/Login")]
    [InlineData("/Registro/Signup")]
    [InlineData("/Registro/SignupJuez")]
    public async Task LaMarcaDiceConcursoDeAltares(string ruta)
    {
        var html = await _factory.CrearCliente().GetStringAsync(ruta);

        Assert.Contains(">Concurso de Altares<", html);
        Assert.DoesNotMatch(">\\s*AltarWeb\\s*<", html);
        // Tambien la pestaña del navegador.
        Assert.Matches("<title>[^<]*- Concurso de Altares</title>", html);
    }
}
