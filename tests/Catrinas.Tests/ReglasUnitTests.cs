using Catrinas.Web;
using Catrinas.Web.Modelos;

namespace Catrinas.Tests;

public class CalculoCatrinaTests
{
    private static readonly ConfiguracionPeriodo PorDefecto = new();

    [Fact]
    public void Nota_AplicaPesosPorDefecto()
    {
        Assert.Equal(6.00m, CalculoCatrina.CalcularNota(10, 8, 6, 4, 2, PorDefecto));
        Assert.Equal(10.00m, CalculoCatrina.CalcularNota(10, 10, 10, 10, 10, PorDefecto));
        Assert.Equal(0m, CalculoCatrina.CalcularNota(0, 0, 0, 0, 0, PorDefecto));
    }

    [Fact]
    public void PesosValidos_SoloSiSuman100YNoSonNegativos()
    {
        Assert.True(CalculoCatrina.PesosValidos(PorDefecto));
        Assert.False(CalculoCatrina.PesosValidos(new ConfiguracionPeriodo { PesoVestuario = 0.30m }));
        Assert.False(CalculoCatrina.PesosValidos(new ConfiguracionPeriodo { PesoVestuario = 0.35m, PesoMaquillaje = -0.10m, PesoAccesorios = 0.25m }));
    }

    // Sin criterio de desempate definido: notas iguales comparten lugar (1, 1, 3) y no se usa ningun
    // campo incidental (Id, fecha) para romper el empate.
    [Fact]
    public void Lugares_CompartenEmpates_YSoloHastaTercero()
    {
        var lugares = CalculoCatrina.CalcularLugares(new[] { (10, 9.5m), (20, 9.5m), (30, 8m), (40, 7m), (5, 7m) });
        Assert.Equal(1, lugares[10]);
        Assert.Equal(1, lugares[20]);
        Assert.Equal(3, lugares[30]);
        Assert.Null(lugares[40]);
        Assert.Null(lugares[5]);

        var tresEmpatados = CalculoCatrina.CalcularLugares(new[] { (1, 8m), (2, 8m), (3, 8m), (4, 7m) });
        Assert.All(new[] { 1, 2, 3 }, id => Assert.Equal(1, tresEmpatados[id]));
        Assert.Null(tresEmpatados[4]);
    }
}

public class ReferenciaAltarTests
{
    [Theory]
    [InlineData("ALT-2026-2-0042", "ALT-2026-2-0042")]
    [InlineData(" alt-2026-1-0001 ", "ALT-2026-1-0001")]
    [InlineData("ALT-2026-2-12345", "ALT-2026-2-12345")]
    public void FormatosValidos_SeNormalizan(string entrada, string esperado)
    {
        var (id, _, error) = ReferenciaAltar.Normalizar(entrada, null);
        Assert.Null(error);
        Assert.Equal(esperado, id);
    }

    [Theory]
    [InlineData("ALT-2026-3-0042")]
    [InlineData("ALT-26-2-0042")]
    [InlineData("ALT-2026-2-42")]
    [InlineData("CAT-2026-2-0042")]
    [InlineData("ALT-2026-2-00000000001")]
    [InlineData("ALT-2026-2-0042 OR 1=1")]
    public void FormatosInvalidos_SeRechazan(string entrada)
    {
        Assert.NotNull(ReferenciaAltar.Normalizar(entrada, null).Error);
    }

    [Fact]
    public void SinReferencia_EsValido_YNombreSinCodigoNo()
    {
        Assert.Equal((null, null, null), ReferenciaAltar.Normalizar("  ", null));
        Assert.NotNull(ReferenciaAltar.Normalizar(null, "Equipo").Error);
        Assert.NotNull(ReferenciaAltar.Normalizar("ALT-2026-2-0001", new string('x', 151)).Error);
    }
}

public class ReglasInscripcionTests
{
    private static Inscripcion Con(params RolInscripcion[] roles)
    {
        var i = new Inscripcion { Nombre = "Catrina", CarreraId = 1 };
        foreach (var rol in roles) i.Miembros.Add(new MiembroInscripcion { Rol = rol });
        return i;
    }

    [Fact]
    public void Final_ExigeExactamenteUnPersonificador_MaestroOpcional()
    {
        Assert.True(ReglasInscripcion.EstaListaParaFinal(Con(RolInscripcion.Personificador, RolInscripcion.Integrante), out _));
        Assert.False(ReglasInscripcion.EstaListaParaFinal(Con(RolInscripcion.Integrante), out var motivo));
        Assert.Contains("personifica", motivo);
        Assert.False(ReglasInscripcion.EstaListaParaFinal(Con(RolInscripcion.Personificador, RolInscripcion.Personificador), out _));
    }
}
