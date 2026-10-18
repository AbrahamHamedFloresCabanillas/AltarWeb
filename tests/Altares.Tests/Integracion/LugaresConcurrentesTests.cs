using AltarWeb.Models;
using AltarWeb.Models.Registro;
using AltarWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Equipo = AltarWeb.Models.Registro.Equipo;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace Altares.Tests.Integracion;

// Hallazgo de la prueba de carga (F4): cada cierre Final recalcula los lugares de su carrera leyendo su
// propia foto; sin serializar, el ultimo en guardar podia dejar lugares viejos. Ahora el recalculo toma
// un bloqueo de aplicacion por carrera+periodo (Concurso.Core.Datos.BloqueoExclusivo).
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class LugaresConcurrentesTests
{
    private readonly AltarWebFactory _factory;
    public LugaresConcurrentesTests(AltarWebFactory factory) => _factory = factory;

    [Fact]
    public async Task CierresFinalSimultaneos_DejanLugaresDeAcuerdoALasNotas()
    {
        const string periodo = "2032-1";
        int carreraId;
        var evaluaciones = new List<int>();
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
            carreraId = await context.CatalogoCarreras.OrderByDescending(c => c.Id).Select(c => c.Id).FirstAsync();
            for (var k = 0; k < 8; k++)
            {
                var equipo = new Equipo
                {
                    Nombre = $"Lugares-{k}", NombreAltar = $"Altar lugares {k}", CarreraId = carreraId, Periodo = periodo,
                    CodigoPublico = $"ALT-{periodo}-L{k:D3}"
                };
                context.EquiposConcurso.Add(equipo);
                await context.SaveChangesAsync();
                var evaluacion = new Evaluacion { EquipoId = equipo.Id, Periodo = periodo, Niveles = 7, NotaFinal = 50 + k };
                context.EvaluacionesConcurso.Add(evaluacion);
                await context.SaveChangesAsync();
                evaluaciones.Add(evaluacion.Id);
            }
        }

        // Cada "juez" cierra su evaluacion como Final y recalcula, todos a la vez y con su propio contexto.
        await Task.WhenAll(evaluaciones.Select(id => Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
            var evaluacion = await context.EvaluacionesConcurso.SingleAsync(e => e.Id == id);
            evaluacion.Estado = EstadoEvaluacion.Final;
            await context.SaveChangesAsync();
            await new EvaluacionCalculoService(context).RecalcularLugaresAsync(carreraId, periodo);
        })));

        using var verificacion = _factory.Services.CreateScope();
        var lugares = await verificacion.ServiceProvider.GetRequiredService<AltarDbContext>().EvaluacionesConcurso.AsNoTracking()
            .Where(e => evaluaciones.Contains(e.Id)).OrderBy(e => e.NotaFinal).Select(e => e.Lugar).ToListAsync();
        Assert.Equal(new int?[] { null, null, null, null, null, 3, 2, 1 }, lugares.ToArray());
    }
}
