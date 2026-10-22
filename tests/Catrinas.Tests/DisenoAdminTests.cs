using System.Text.RegularExpressions;
using Catrinas.Tests.Infraestructura;

namespace Catrinas.Tests;

// B6 (revision manual del propietario, 2026-10-21): Catrinas se alinea al diseño de Altares (la referencia) en la
// administracion: botones de desactivar/reactivar cuentas con los mismos iconos y colores, y pesos de la
// configuracion como enteros ("25", no "25.00") con la suma total en vivo.
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class DisenoAdminTests
{
    private readonly CatrinasFactory _f;
    public DisenoAdminTests(CatrinasFactory f) => _f = f;

    [Fact]
    public async Task BotonesDeCuentas_UsanLosIconosDeAltares()
    {
        await Escenario.CrearAsync(_f, carreraId: 9);
        var admin = await Escenario.JuradoAsync(_f, admin: true);

        foreach (var ruta in new[] { "/Admin/Jurado", "/Admin/Participantes" })
        {
            var html = await admin.GetStringAsync(ruta);
            var desactivar = Regex.Match(html, "<form[^>]*action=\"[^\"]*Desactivar\\w+\"[\\s\\S]*?</form>");
            Assert.True(desactivar.Success, $"Sin boton de desactivar en {ruta}");
            Assert.Contains("fa-user-slash", desactivar.Value);
            Assert.Contains("color:var(--altar-danger)", desactivar.Value);
            Assert.DoesNotContain(">Desactivar<", desactivar.Value);
        }
    }

    [Fact]
    public async Task PesosDeLaConfiguracion_SeMuestranComoEnteros_ConSumaEnVivo()
    {
        var admin = await Escenario.JuradoAsync(_f, admin: true);
        var html = await admin.GetStringAsync("/Admin/Configuracion");

        foreach (Match peso in Regex.Matches(html, "<input[^>]*name=\"Peso\\w+Pct\"[^>]*>"))
        {
            var valor = Regex.Match(peso.Value, "value=\"([^\"]*)\"").Groups[1].Value;
            Assert.Matches("^\\d+$", valor);
            Assert.Contains("altar-peso-input", peso.Value);
        }
        Assert.Contains("id=\"altar-suma-pesos\"", html);
    }
}
