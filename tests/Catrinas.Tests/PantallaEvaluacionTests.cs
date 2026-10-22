using System.Net;
using System.Text.RegularExpressions;
using Catrinas.Tests.Infraestructura;

namespace Catrinas.Tests;

// Pantalla de evaluacion del jurado (revision manual del propietario, 2026-10-21).
// B3: cada criterio debe explicar que se evalua, como lo hace Altares con sus guias.
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class PantallaEvaluacionTests
{
    private readonly CatrinasFactory _f;
    public PantallaEvaluacionTests(CatrinasFactory f) => _f = f;

    private async Task<string> PantallaAsync()
    {
        var e = await Escenario.CrearAsync(_f, carreraId: 8);
        var jurado = await Escenario.JuradoAsync(_f);
        return await jurado.GetStringAsync($"/Evaluacion/Evaluar?inscripcionId={e.InscripcionId}");
    }

    [Fact]
    public async Task CadaCriterio_TieneSuDescripcion()
    {
        var html = await PantallaAsync();

        // Una guia por cada uno de los cinco sliders, dentro de su fila.
        var filas = Regex.Matches(html, "<div class=\"altar-slider-row\">([\\s\\S]*?)<input type=\"range\"[^>]*>\\s*([\\s\\S]*?)</div>\\s*(?=<div class=\"altar-slider-row\">|<div class=\"altar-flex\")");
        Assert.Equal(5, filas.Count);
        foreach (Match fila in filas)
        {
            var guia = Regex.Match(fila.Groups[2].Value, "class=\"altar-slider-hint\">([^<]+)<");
            Assert.True(guia.Success && WebUtility.HtmlDecode(guia.Groups[1].Value).Trim().Length > 20, $"Criterio sin descripcion: {fila.Groups[1].Value.Trim()}");
        }
    }
}
