using System.Net;
using Catrinas.Tests.Infraestructura;
using Concurso.Core.Fechas;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Tests;

// M3 (pruebas adversariales 2026-10-21): la configuracion aceptaba fechas limite del año 0009 (la inscripcion quedo
// cerrada en las pruebas manuales) y una fecha de requisitos anterior a la de inscripcion.
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class ConfiguracionFechasTests
{
    private readonly CatrinasFactory _f;
    public ConfiguracionFechasTests(CatrinasFactory f) => _f = f;

    private static (string, string)[] Formulario(string? inscripcion, string? requisitos) => new[]
    {
        ("FechaLimiteInscripcion", inscripcion ?? string.Empty), ("FechaLimiteRequisitos", requisitos ?? string.Empty),
        ("PesoVestuarioPct", "25"), ("PesoMaquillajePct", "20"), ("PesoAccesoriosPct", "10"),
        ("PesoOriginalidadCreatividadPct", "20"), ("PesoApegoTradicionesPct", "25"), ("UmbralAgrupacionDemografica", "5")
    };

    private (DateTime? Inscripcion, DateTime? Requisitos) Fechas()
    {
        var periodo = PeriodoHelper.ObtenerPeriodoActual();
        return _f.ConContexto(c => c.Configuraciones.AsNoTracking().Where(x => x.Periodo == periodo)
            .Select(x => new { x.FechaLimiteInscripcion, x.FechaLimiteRequisitos }).AsEnumerable()
            .Select(x => (x.FechaLimiteInscripcion, x.FechaLimiteRequisitos)).FirstOrDefault());
    }

    private void Restaurar((DateTime? Inscripcion, DateTime? Requisitos) fechas)
    {
        var periodo = PeriodoHelper.ObtenerPeriodoActual();
        _f.ConContexto(c =>
        {
            var config = c.Configuraciones.FirstOrDefault(x => x.Periodo == periodo);
            if (config == null) return 0;
            config.FechaLimiteInscripcion = fechas.Inscripcion;
            config.FechaLimiteRequisitos = fechas.Requisitos;
            return c.SaveChanges();
        });
    }

    [Theory]
    [InlineData("0009-09-09", null)]
    [InlineData(null, "0009-09-09")]
    [InlineData("2210-01-01", null)]
    [InlineData("2026-11-01", "2026-10-25")]
    public async Task FechasAbsurdas_SeRechazan(string? inscripcion, string? requisitos)
    {
        var admin = await Escenario.JuradoAsync(_f, admin: true);
        var antes = Fechas();
        try
        {
            var respuesta = await Http.PostDesdeAsync(admin, "/Admin/Configuracion", "/Admin/Configuracion", Formulario(inscripcion, requisitos));

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Equal(antes, Fechas());
        }
        finally
        {
            Restaurar(antes);
        }
    }

    [Fact]
    public async Task FechasCoherentes_SeGuardan()
    {
        var admin = await Escenario.JuradoAsync(_f, admin: true);
        var antes = Fechas();
        try
        {
            Http.Redirige(await Http.PostDesdeAsync(admin, "/Admin/Configuracion", "/Admin/Configuracion", Formulario("2026-10-25", "2026-10-25")), "Configuracion");
            Assert.Equal(new DateTime(2026, 10, 25), Fechas().Requisitos);
        }
        finally
        {
            Restaurar(antes);
        }
    }
}
