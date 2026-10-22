using System.Net;
using Catrinas.Tests.Infraestructura;

namespace Catrinas.Tests;

// B2 (revision manual del propietario, 2026-10-21/22): la carrera Id 2 se llama "Ing. en Bioingeniería" (antes
// "Bioingeniero"). El cambio llega por migracion de datos, asi que tambien corrige las bases ya creadas.
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class CatalogoCarrerasTests
{
    private const string Nombre = "Ing. en Bioingeniería";
    private readonly CatrinasFactory _f;
    public CatalogoCarrerasTests(CatrinasFactory f) => _f = f;

    [Fact]
    public async Task Bioingenieria_TieneElNombreAcordado_EnLaBaseYEnElRegistro()
    {
        Assert.Equal(Nombre, _f.ConContexto(c => c.Carreras.Single(x => x.Id == 2).Nombre));

        var signup = WebUtility.HtmlDecode(await _f.Cliente().GetStringAsync("/Registro/Signup"));
        Assert.Contains($">{Nombre}<", signup);
        Assert.DoesNotContain(">Bioingeniero<", signup);
    }
}
