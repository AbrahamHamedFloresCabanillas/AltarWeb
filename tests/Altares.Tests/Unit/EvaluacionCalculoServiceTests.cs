using AltarWeb.Models;
using AltarWeb.Models.Registro;
using AltarWeb.Services;
using Microsoft.EntityFrameworkCore;
using Equipo = AltarWeb.Models.Registro.Equipo;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace Altares.Tests.Unit;

// Formulas de vision.md 7.2, 7.5 y 7.7 con la configuracion por defecto de ConfiguracionPeriodo.
public class EvaluacionCalculoServiceTests
{
    private static readonly ConfiguracionPeriodo Config = new() { Periodo = "2026-2" };

    private static ElementoEvaluado Evaluado(CategoriaElemento categoria, Satisfaccion satisfaccion, bool tematizado = false) => new()
    {
        Elemento = new Elemento { Categoria = categoria },
        Satisfaccion = satisfaccion,
        Tematizado = tematizado
    };

    [Theory]
    [InlineData(Satisfaccion.NoLoTiene, 0.0)]
    [InlineData(Satisfaccion.Regular, 0.5)]
    [InlineData(Satisfaccion.Bueno, 1.0)]
    public void ValorDeSatisfaccion_UsaLaEscalaConfigurada(Satisfaccion satisfaccion, double esperado)
    {
        Assert.Equal((decimal)esperado, EvaluacionCalculoService.ValorDeSatisfaccion(satisfaccion, Config));
    }

    [Fact]
    public void PuntajeElementos_TodoBueno_Es10()
    {
        var servicio = new EvaluacionCalculoService(null!);
        var elementos = new[]
        {
            Evaluado(CategoriaElemento.RitualObligatorio, Satisfaccion.Bueno),
            Evaluado(CategoriaElemento.Decorativo, Satisfaccion.Bueno)
        };

        Assert.Equal(10m, servicio.CalcularPuntajeElementos(elementos, Config));
    }

    [Fact]
    public void PuntajeElementos_PonderaRitualSobreDecorativo()
    {
        var servicio = new EvaluacionCalculoService(null!);
        // Ritual Bueno (1.0 * 1.0) + Decorativo No lo tiene (0 * 0.5) sobre maximo 1.5 => 6.67
        var elementos = new[]
        {
            Evaluado(CategoriaElemento.RitualObligatorio, Satisfaccion.Bueno),
            Evaluado(CategoriaElemento.Decorativo, Satisfaccion.NoLoTiene)
        };

        Assert.Equal(6.67m, servicio.CalcularPuntajeElementos(elementos, Config));
    }

    [Fact]
    public void PuntajeElementos_SinElementos_Es0()
    {
        var servicio = new EvaluacionCalculoService(null!);
        Assert.Equal(0m, servicio.CalcularPuntajeElementos(Array.Empty<ElementoEvaluado>(), Config));
    }

    [Fact]
    public void Creatividad_SumaBonusPorTematizadoConTope10()
    {
        var servicio = new EvaluacionCalculoService(null!);
        Assert.Equal(8.5m, servicio.CalcularEsenciaPersonalidadFinal(8m, 2, Config));
        Assert.Equal(10m, servicio.CalcularEsenciaPersonalidadFinal(9.9m, 4, Config));
    }

    [Fact]
    public void NotaFinal_AplicaPesos30_30_20_10_10()
    {
        var servicio = new EvaluacionCalculoService(null!);
        var eval = new Evaluacion
        {
            ObjetivoCultural = 10m,
            EsenciaPersonalidad = 5m,
            EsenciaPersonalidadFinal = 8m, // la final (con bonus) prevalece sobre la cruda
            ValoracionGeneral = 6m,
            DistribucionNiveles = 4m,
            Narrador = 2m
        };

        // 10*.3 + 8*.3 + 6*.2 + 4*.1 + 2*.1 = 3 + 2.4 + 1.2 + 0.4 + 0.2
        Assert.Equal(7.2m, servicio.CalcularNotaFinal(eval, Config));
    }

    [Fact]
    public void NotaCatrina_AplicaPesos25_20_10_20_25()
    {
        var servicio = new EvaluacionCalculoService(null!);
        var catrina = new EvaluacionCatrina
        {
            Vestuario = 10m,
            Maquillaje = 8m,
            Accesorios = 6m,
            OriginalidadCreatividad = 4m,
            ApegoTradiciones = 2m
        };

        // 2.5 + 1.6 + 0.6 + 0.8 + 0.5
        Assert.Equal(6.0m, servicio.CalcularNotaCatrina(catrina, Config));
    }

    [Fact]
    public async Task RecalcularLugares_RankeaSoloFinalesDeLaMismaCarreraYPeriodo()
    {
        await using var context = CrearContextoEnMemoria();
        var sistemas = new CatalogoCarrera { Id = 1, Nombre = "Sistemas" };
        var civil = new CatalogoCarrera { Id = 2, Nombre = "Civil" };
        context.CatalogoCarreras.AddRange(sistemas, civil);

        Evaluacion Agregar(int id, CatalogoCarrera carrera, string periodo, EstadoEvaluacion estado, decimal nota, decimal? notaCatrina = null)
        {
            var equipo = new Equipo { Id = id, Nombre = $"E{id}", NombreAltar = $"A{id}", Carrera = carrera, Periodo = periodo };
            var eval = new Evaluacion { Id = id, Equipo = equipo, Periodo = periodo, Estado = estado, NotaFinal = nota };
            if (notaCatrina != null) eval.EvaluacionCatrina = new EvaluacionCatrina { Id = id, NotaCatrina = notaCatrina };
            context.EquiposConcurso.Add(equipo);
            context.EvaluacionesConcurso.Add(eval);
            return eval;
        }

        var e1 = Agregar(1, sistemas, "2026-2", EstadoEvaluacion.Final, 9.0m, notaCatrina: 5m);
        var e2 = Agregar(2, sistemas, "2026-2", EstadoEvaluacion.Final, 9.5m, notaCatrina: 8m);
        var e3 = Agregar(3, sistemas, "2026-2", EstadoEvaluacion.Final, 7.0m);
        var e4 = Agregar(4, sistemas, "2026-2", EstadoEvaluacion.Final, 6.0m);
        var preliminar = Agregar(5, sistemas, "2026-2", EstadoEvaluacion.Preliminar, 10m);
        var otraCarrera = Agregar(6, civil, "2026-2", EstadoEvaluacion.Final, 10m);
        var otroPeriodo = Agregar(7, sistemas, "2026-1", EstadoEvaluacion.Final, 10m);
        await context.SaveChangesAsync();

        await new EvaluacionCalculoService(context).RecalcularLugaresAsync(sistemas.Id, "2026-2");

        Assert.Equal(1, e2.Lugar);
        Assert.Equal(2, e1.Lugar);
        Assert.Equal(3, e3.Lugar);
        Assert.Null(e4.Lugar);
        Assert.Null(preliminar.Lugar);
        Assert.Null(otraCarrera.Lugar);
        Assert.Null(otroPeriodo.Lugar);

        // Ranking de Catrina independiente del ranking del altar.
        Assert.Equal(1, e2.EvaluacionCatrina!.LugarCatrina);
        Assert.Equal(2, e1.EvaluacionCatrina!.LugarCatrina);
    }

    private static AltarDbContext CrearContextoEnMemoria() =>
        new(new DbContextOptionsBuilder<AltarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
