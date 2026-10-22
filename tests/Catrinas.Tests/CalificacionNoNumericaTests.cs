using System.Net;
using Catrinas.Tests.Infraestructura;

namespace Catrinas.Tests;

// Hallazgo bajo de las pruebas adversariales (2026-10-21): con una peticion alterada, un criterio no numerico
// ("abc") se guardaba como 0 en vez de rechazarse (el enlace de modelo deja 0 y nadie revisaba ModelState).
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class CalificacionNoNumericaTests
{
    private readonly CatrinasFactory _f;
    public CalificacionNoNumericaTests(CatrinasFactory f) => _f = f;

    [Theory]
    [InlineData("vestuario")]
    [InlineData("apegoTradiciones")]
    public async Task CriterioNoNumerico_SeRechaza(string campo)
    {
        var e = await Escenario.CrearAsync(_f, carreraId: 10);
        var jurado = await Escenario.JuradoAsync(_f);
        var ruta = $"/Evaluacion/Evaluar?inscripcionId={e.InscripcionId}";
        var campos = new Dictionary<string, string>
        {
            ["inscripcionId"] = e.InscripcionId.ToString(), ["vestuario"] = "5", ["maquillaje"] = "5", ["accesorios"] = "5",
            ["originalidadCreatividad"] = "5", ["apegoTradiciones"] = "5", ["accion"] = "preliminar", ["confirmacion"] = "", ["rowVersion"] = ""
        };
        campos[campo] = "abc";

        var respuesta = await Http.PostDesdeAsync(jurado, ruta, "/Evaluacion/Guardar", campos.Select(c => (c.Key, c.Value)).ToArray());

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Null(Escenario.Evaluacion(_f, e.InscripcionId));
        Assert.Contains("Cada criterio debe calificarse entre 0 y 10", WebUtility.HtmlDecode(await jurado.GetStringAsync(respuesta.Headers.Location!.OriginalString)));
    }
}
