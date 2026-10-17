using System.IO.Compression;
using AltarWeb.Models.Registro;
using AltarWeb.Services;
using Microsoft.Extensions.DependencyInjection;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace Altares.Tests.Integracion;

// QuestPDF depende de binarios nativos (Skia); esta prueba confirma que la generacion real funciona
// en el runtime actual, con los logos de wwwroot y el reporte de cierre con datos reales de la BD.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class PdfTests
{
    private readonly AltarWebFactory _factory;

    public PdfTests(AltarWebFactory factory) => _factory = factory;

    private static Evaluacion EvaluacionDePrueba() => new()
    {
        SnapshotNombreEquipo = "Equipo de prueba",
        SnapshotNombreAltar = "Altar de prueba",
        SnapshotDifuntoNombre = "Difunto de prueba",
        Lugar = 1,
        Integrantes =
        {
            new EvaluacionIntegrante { NombreCompleto = "Integrante Uno", Identificador = "1900001" },
            new EvaluacionIntegrante { NombreCompleto = "Integrante Dos", Identificador = "1900002" }
        }
    };

    private static void AssertEsPdf(byte[] contenido)
    {
        Assert.True(contenido.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(contenido, 0, 4));
    }

    [Fact]
    public void Constancias_GeneranPdfYZip()
    {
        using var scope = _factory.Services.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<ConstanciaService>();
        var eval = EvaluacionDePrueba();

        AssertEsPdf(servicio.GenerarGrupal(eval));
        AssertEsPdf(servicio.GenerarMaestroEncargado(eval, "Maestro"));
        AssertEsPdf(servicio.GenerarJuez(eval, "Juez"));

        using var zip = new ZipArchive(new MemoryStream(servicio.GenerarZipIndividuales(eval)));
        Assert.Equal(2, zip.Entries.Count);
    }

    [Fact]
    public async Task ReporteDeCierre_GeneraPdf()
    {
        using var scope = _factory.Services.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<ReportePeriodoService>();

        var reporte = await servicio.GenerarReporteAsync("2026-2");
        AssertEsPdf(servicio.GenerarPdf(reporte, "pruebas"));
    }
}
