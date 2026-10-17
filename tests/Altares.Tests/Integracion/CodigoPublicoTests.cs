using System.Text.RegularExpressions;
using AltarWeb.Models;
using AltarWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Equipo = AltarWeb.Models.Registro.Equipo;

namespace Altares.Tests.Integracion;

// CodigoPublico de equipos (F2, ADR §2.6): ALT-{Periodo}-{NNNN}, unico, inmutable y sin colisiones bajo
// concurrencia (contador por periodo incrementado atomicamente dentro de la transaccion del equipo).
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class CodigoPublicoTests
{
    private readonly AltarWebFactory _factory;

    public CodigoPublicoTests(AltarWebFactory factory) => _factory = factory;

    [Fact]
    public void Formato_EsAltPeriodoConsecutivoDe4Digitos()
    {
        Assert.Equal("ALT-2026-2-0042", CodigoPublicoEquipo.Formatear("2026-2", 42));
        Assert.Equal("ALT-2026-1-12345", CodigoPublicoEquipo.Formatear("2026-1", 12345));
    }

    private async Task<Equipo> CrearEquipoAsync(string periodo, string nombre)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
        var carreraId = await context.CatalogoCarreras.Select(c => c.Id).FirstAsync();

        await using var transaccion = await context.Database.BeginTransactionAsync();
        var equipo = new Equipo { Nombre = nombre, NombreAltar = "Altar " + nombre, CarreraId = carreraId, Periodo = periodo };
        equipo.CodigoPublico = await CodigoPublicoEquipo.SiguienteAsync(context, periodo);
        context.EquiposConcurso.Add(equipo);
        await context.SaveChangesAsync();
        await transaccion.CommitAsync();
        return equipo;
    }

    [Fact]
    public async Task Secuencia_EsPorPeriodo_YUnica()
    {
        var a = await CrearEquipoAsync("2031-1", "Secuencia-A");
        var b = await CrearEquipoAsync("2031-1", "Secuencia-B");
        var c = await CrearEquipoAsync("2031-2", "Secuencia-C");

        Assert.Equal("ALT-2031-1-0001", a.CodigoPublico);
        Assert.Equal("ALT-2031-1-0002", b.CodigoPublico);
        Assert.Equal("ALT-2031-2-0001", c.CodigoPublico);
    }

    // Concurrencia: 25 creaciones simultaneas del mismo periodo, cada una con su propio DbContext y su
    // propia transaccion, reciben exactamente los consecutivos 1..25, sin duplicados ni huecos.
    [Fact]
    public async Task CreacionesConcurrentes_NoProducenDuplicados()
    {
        var equipos = await Task.WhenAll(Enumerable.Range(0, 25).Select(i => Task.Run(() => CrearEquipoAsync("2032-1", $"Concurrente-{i}"))));

        var codigos = equipos.Select(e => e.CodigoPublico).ToList();
        Assert.Equal(25, codigos.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 25).Select(n => CodigoPublicoEquipo.Formatear("2032-1", n)).OrderBy(x => x), codigos.OrderBy(x => x));
    }

    // Si la creacion del equipo se revierte, el consecutivo tambien: no quedan huecos.
    [Fact]
    public async Task TransaccionRevertida_NoConsumeConsecutivo()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
            await using var transaccion = await context.Database.BeginTransactionAsync();
            await CodigoPublicoEquipo.SiguienteAsync(context, "2033-1");
            await transaccion.RollbackAsync();
        }

        Assert.Equal("ALT-2033-1-0001", (await CrearEquipoAsync("2033-1", "Tras-Rollback")).CodigoPublico);
    }

    [Fact]
    public async Task SinTransaccion_NoGeneraCodigo()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => CodigoPublicoEquipo.SiguienteAsync(context, "2034-1"));
    }

    [Fact]
    public async Task CodigoGuardado_EsInmutable()
    {
        var equipo = await CrearEquipoAsync("2035-1", "Inmutable");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
        var cargado = await context.EquiposConcurso.FirstAsync(e => e.Id == equipo.Id);
        cargado.CodigoPublico = "ALT-2035-1-9999";

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task IndiceUnico_RechazaDuplicadosEnLaBase()
    {
        var equipo = await CrearEquipoAsync("2036-1", "Unico-A");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
        context.EquiposConcurso.Add(new Equipo
        {
            Nombre = "Unico-B", NombreAltar = "X", Periodo = "2036-1", CodigoPublico = equipo.CodigoPublico,
            CarreraId = await context.CatalogoCarreras.Select(c => c.Id).FirstAsync()
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}

// La migracion AgregarCodigoPublicoAEquipos asigna codigo a los equipos que ya existian (activos e
// inactivos), deja los contadores listos para continuar y no toca datos de Catrina.
[Trait("Category", "Integracion")]
public class MigracionCodigoPublicoTests
{
    private const string MigracionAnterior = "20260911205005_ValidacionAdminDeFichaDeRegistro";

    [Fact]
    public async Task EquiposExistentes_RecibenCodigoYLosContadoresContinuan()
    {
        var cadena = $"Server=(localdb)\\MSSQLLocalDB;Database=AltarWebMigracion_{Guid.NewGuid():N};Trusted_Connection=True;MultipleActiveResultSets=true";
        var opciones = new DbContextOptionsBuilder<AltarDbContext>().UseSqlServer(cadena).Options;

        await using var context = new AltarDbContext(opciones);
        try
        {
            await context.GetService<IMigrator>().MigrateAsync(MigracionAnterior);

            await context.Database.ExecuteSqlRawAsync(@"
DECLARE @carrera int = (SELECT TOP 1 Id FROM CatalogoCarreras ORDER BY Id);
INSERT INTO EquiposConcurso (Nombre, NombreAltar, CarreraId, Periodo, Activo, CreadoEn, HaceCatrina)
VALUES (N'Viejo-1', N'A', @carrera, '2026-1', 1, SYSDATETIME(), 0),
       (N'Viejo-2', N'B', @carrera, '2026-2', 1, SYSDATETIME(), 1),
       (N'Viejo-3', N'C', @carrera, '2026-2', 0, SYSDATETIME(), 0),
       (N'Viejo-4', N'D', @carrera, '2026-2', 1, SYSDATETIME(), 0);");

            await context.Database.MigrateAsync();

            var codigos = await context.EquiposConcurso.IgnoreQueryFilters()
                .OrderBy(e => e.Id).Select(e => new { e.Nombre, e.CodigoPublico, e.HaceCatrina }).ToListAsync();
            Assert.Equal(new[] { "ALT-2026-1-0001", "ALT-2026-2-0001", "ALT-2026-2-0002", "ALT-2026-2-0003" },
                codigos.Select(c => c.CodigoPublico));
            // Columna legada de Catrina intacta.
            Assert.True(codigos.Single(c => c.Nombre == "Viejo-2").HaceCatrina);

            // El siguiente equipo del periodo continua el consecutivo.
            await using var transaccion = await context.Database.BeginTransactionAsync();
            Assert.Equal("ALT-2026-2-0004", await CodigoPublicoEquipo.SiguienteAsync(context, "2026-2"));
            await transaccion.RollbackAsync();
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}
