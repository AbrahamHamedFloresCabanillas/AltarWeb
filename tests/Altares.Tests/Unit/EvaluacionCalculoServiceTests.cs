using AltarWeb.Models;
using AltarWeb.Models.Registro;
using AltarWeb.Services;
using Microsoft.EntityFrameworkCore;
using Equipo = AltarWeb.Models.Registro.Equipo;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace Altares.Tests.Unit;

// Formulas de vision.md 7.2 y 7.7 con la configuracion por defecto de ConfiguracionPeriodo.
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
    public async Task RecalcularLugares_RankeaSoloFinalesDeLaMismaCarreraYPeriodo_YNoTocaCatrina()
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

        // F2: Altares ya no calcula el ranking de Catrina; los datos historicos no se tocan.
        Assert.Null(e2.EvaluacionCatrina!.LugarCatrina);
        Assert.Null(e1.EvaluacionCatrina!.LugarCatrina);
    }

    // C4 (pruebas adversariales 2026-10-21): tres notas identicas quedaban 1º, 2º y 3º segun el orden en que las
    // devolvia la base. Decision del propietario (hibrido): desempata la Creatividad (criterio provisional hasta que
    // el comite lo ratifique, vision.md §13.11); si persiste el empate, comparten lugar y se marca para el comite.
    [Fact]
    public async Task RecalcularLugares_EmpateEnNota_DesempataLaCreatividad_YSiPersisteCompartenLugar()
    {
        await using var context = CrearContextoEnMemoria();
        var carrera = new CatalogoCarrera { Id = 1, Nombre = "Sistemas" };
        context.CatalogoCarreras.Add(carrera);

        Evaluacion Agregar(int id, decimal nota, decimal creatividadFinal)
        {
            var equipo = new Equipo { Id = id, Nombre = $"E{id}", NombreAltar = $"A{id}", Carrera = carrera, Periodo = "2026-2" };
            var eval = new Evaluacion
            {
                Id = id, Equipo = equipo, Periodo = "2026-2", Estado = EstadoEvaluacion.Final, NotaFinal = nota,
                EsenciaPersonalidad = creatividadFinal, EsenciaPersonalidadFinal = creatividadFinal
            };
            context.EquiposConcurso.Add(equipo);
            context.EvaluacionesConcurso.Add(eval);
            return eval;
        }

        var masCreativo = Agregar(1, 8.5m, 9m);
        var empatadoA = Agregar(2, 8.5m, 7m);
        var empatadoB = Agregar(3, 8.5m, 7m);
        var cuarto = Agregar(4, 8.0m, 10m);
        await context.SaveChangesAsync();

        await new EvaluacionCalculoService(context).RecalcularLugaresAsync(carrera.Id, "2026-2");

        Assert.Equal(1, masCreativo.Lugar);
        Assert.Equal(2, empatadoA.Lugar);
        Assert.Equal(2, empatadoB.Lugar);
        Assert.Null(cuarto.Lugar); // 1, 2, 2: el siguiente seria 4º
        var finales = new[] { masCreativo, empatadoA, empatadoB, cuarto };
        Assert.False(EvaluacionCalculoService.TieneEmpateSinResolver(masCreativo, finales));
        Assert.True(EvaluacionCalculoService.TieneEmpateSinResolver(empatadoA, finales));
        Assert.True(EvaluacionCalculoService.TieneEmpateSinResolver(empatadoB, finales));
        Assert.False(EvaluacionCalculoService.TieneEmpateSinResolver(cuarto, finales));
    }

    private static AltarDbContext CrearContextoEnMemoria() =>
        new(new DbContextOptionsBuilder<AltarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
