using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using AltarWeb.Models.Registro;
using AltarWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Equipo = AltarWeb.Models.Registro.Equipo;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace Altares.Tests.Integracion;

// C4 (decision hibrida del propietario, 2026-10-21): un empate que el criterio de desempate no resuelve comparte
// lugar y se marca en Historial y Detalle para que el comite lo resuelva.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class EmpateLugaresTests
{
    private const string Marca = "Empate: lo resuelve el comité";
    private readonly AltarWebFactory _factory;
    public EmpateLugaresTests(AltarWebFactory factory) => _factory = factory;

    [Fact]
    public async Task EmpateSinResolver_SeMarcaEnHistorialYDetalle()
    {
        var periodo = Concurso.Core.Fechas.PeriodoHelper.ObtenerPeriodoActual();
        var ids = new List<int>();
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
            // Carrera propia para no competir con evaluaciones de otras pruebas del periodo actual.
            var carrera = await context.CatalogoCarreras.OrderByDescending(c => c.Id).Skip(2).Select(c => c.Id).FirstAsync();
            foreach (var (nombre, creatividad) in new[] { ("C4-A", 7m), ("C4-B", 7m), ("C4-C", 9m) })
            {
                var equipo = new Equipo { Nombre = nombre, NombreAltar = $"Altar {nombre}", CarreraId = carrera, Periodo = periodo, CodigoPublico = $"ALT-EMP-{nombre}" };
                context.EquiposConcurso.Add(equipo);
                await context.SaveChangesAsync();
                var eval = new Evaluacion
                {
                    EquipoId = equipo.Id, Periodo = periodo, Niveles = 7, NotaFinal = 9.99m, Estado = EstadoEvaluacion.Final,
                    EsenciaPersonalidad = creatividad, EsenciaPersonalidadFinal = creatividad
                };
                context.EvaluacionesConcurso.Add(eval);
                await context.SaveChangesAsync();
                ids.Add(eval.Id);
            }
            await new EvaluacionCalculoService(context).RecalcularLugaresAsync(carrera, periodo);
        }

        await _factory.CrearJuezAsync("juez-empate", "Juez-Empate-2026!");
        var juez = _factory.CrearCliente();
        var html = await juez.GetStringAsync("/Registro/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        await juez.PostAsync("/Acceso/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["usuario"] = "juez-empate", ["password"] = "Juez-Empate-2026!", ["__RequestVerificationToken"] = token
        }));

        string Texto(string h) => WebUtility.HtmlDecode(h);
        Assert.Contains(Marca, Texto(await juez.GetStringAsync($"/AltarEvaluacion/Detalle/{ids[0]}")));
        Assert.Contains(Marca, Texto(await juez.GetStringAsync($"/AltarEvaluacion/Detalle/{ids[1]}")));
        Assert.DoesNotContain(Marca, Texto(await juez.GetStringAsync($"/AltarEvaluacion/Detalle/{ids[2]}")));

        var historial = Texto(await juez.GetStringAsync("/AltarEvaluacion/Historial"));
        // Al menos los dos de esta prueba (el historial muestra todos los periodos de la base compartida).
        Assert.True(Regex.Matches(historial, Regex.Escape(Marca)).Count >= 2);
    }
}
