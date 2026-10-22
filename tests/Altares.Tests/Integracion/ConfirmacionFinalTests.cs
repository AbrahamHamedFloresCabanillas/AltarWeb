using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Equipo = AltarWeb.Models.Registro.Equipo;

namespace Altares.Tests.Integracion;

// B4 (revision manual del propietario, 2026-10-21): en Altares la confirmacion para cerrar como Final era solo del
// navegador (modal); un POST con accion=final sin pasar por el modal cerraba igual. Ahora el servidor exige la
// confirmacion explicita, como Catrinas. El flujo completo con confirmacion lo cubre FlujoAltaresTests.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class ConfirmacionFinalTests
{
    private const string MensajeConfirmacion = "Para cerrar como Final confirma explícitamente";
    private readonly AltarWebFactory _factory;
    public ConfirmacionFinalTests(AltarWebFactory factory) => _factory = factory;

    private async Task<(HttpClient Juez, int EquipoId)> PreparaAsync(string sufijo)
    {
        await _factory.CrearJuezAsync($"juez-confirmacion-{sufijo}", "Juez-Confirmacion-2026!");
        var juez = _factory.CrearCliente();
        var html = await juez.GetStringAsync("/Registro/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        await juez.PostAsync("/Acceso/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["usuario"] = $"juez-confirmacion-{sufijo}", ["password"] = "Juez-Confirmacion-2026!", ["__RequestVerificationToken"] = token
        }));

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
        var equipo = new Equipo
        {
            Nombre = $"Equipo confirmacion {sufijo}", NombreAltar = "Altar confirmacion",
            CarreraId = await context.CatalogoCarreras.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync(),
            Periodo = Concurso.Core.Fechas.PeriodoHelper.ObtenerPeriodoActual(), CodigoPublico = $"ALT-CONF-{sufijo}"
        };
        context.EquiposConcurso.Add(equipo);
        await context.SaveChangesAsync();
        return (juez, equipo.Id);
    }

    private static async Task<string> CerrarFinalAsync(HttpClient juez, int equipoId, string? confirmacion)
    {
        var ruta = $"/AltarEvaluacion/NuevaEvaluacion?equipoId={equipoId}";
        var html = await juez.GetStringAsync(ruta);
        var campos = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value,
            ["equipoId"] = equipoId.ToString(), ["niveles"] = "7", ["tipoAltar"] = "Tradicional", ["semblanza"] = "x",
            ["esenciaPersonalidad"] = "5", ["valoracionGeneral"] = "5", ["distribucionNiveles"] = "5", ["narrador"] = "5",
            ["accion"] = "final", ["version"] = Regex.Match(html, "name=\"version\" value=\"([^\"]*)\"").Groups[1].Value
        };
        if (confirmacion != null) campos["confirmacion"] = confirmacion;
        var respuesta = await juez.PostAsync("/AltarEvaluacion/GuardarEvaluacion", new FormUrlEncodedContent(campos));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return WebUtility.HtmlDecode(await juez.GetStringAsync(respuesta.Headers.Location!.OriginalString));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SI")]
    public async Task Final_SinConfirmacionExplicita_SeRechaza(string? confirmacion)
    {
        var (juez, equipoId) = await PreparaAsync($"{confirmacion ?? "nula"}-{Guid.NewGuid():N}"[..20]);

        var pantalla = await CerrarFinalAsync(juez, equipoId, confirmacion);

        Assert.Contains(MensajeConfirmacion, pantalla);
        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AltarDbContext>().EvaluacionesConcurso.AnyAsync(e => e.EquipoId == equipoId));
    }

    [Fact]
    public async Task Final_ConConfirmacion_PasaAlSiguienteCandado()
    {
        // El equipo no tiene ficha completa: con la confirmacion, el rechazo ya es por la ficha, no por la confirmacion.
        var (juez, equipoId) = await PreparaAsync($"ok-{Guid.NewGuid():N}"[..20]);

        var pantalla = await CerrarFinalAsync(juez, equipoId, "CONFIRMO");

        Assert.DoesNotContain(MensajeConfirmacion, pantalla);
        Assert.Contains("No se puede cerrar como Final", pantalla);
    }
}
