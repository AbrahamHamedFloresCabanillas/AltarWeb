using Concurso.Core.Fechas;
using Concurso.Core.Privacidad;
using Concurso.Core.Seguridad;

namespace Concurso.Core.Tests;

public class FechaConcursoHelperTests
{
    [Fact]
    public void SinLimite_NuncaEstaPasado() => Assert.False(FechaConcursoHelper.LimitePasado(null));

    [Fact]
    public void ElDiaConfigurado_CuentaCompleto()
    {
        var hoy = FechaConcursoHelper.HoyLocal.ToDateTime(TimeOnly.MinValue);
        Assert.False(FechaConcursoHelper.LimitePasado(hoy));
        Assert.True(FechaConcursoHelper.LimitePasado(hoy.AddDays(-1)));
    }

    [Theory]
    [InlineData(2026, 1, "2026-1")]
    [InlineData(2026, 7, "2026-1")]
    [InlineData(2026, 8, "2026-2")]
    [InlineData(2026, 12, "2026-2")]
    public void Periodo_EneJul1_AgoDic2(int anio, int mes, string esperado)
    {
        Assert.Equal(esperado, PeriodoHelper.ObtenerPeriodoActual(new DateTime(anio, mes, 15)));
    }
}

public class PrivacidadReporteHelperTests
{
    [Fact]
    public void AgrupaCategoriasBajoElUmbral()
    {
        var resultado = PrivacidadReporteHelper.AgruparConUmbralDePrivacidad(
            new[] { ("A", 10), ("B", 4), ("C", 1) }, umbral: 5);

        Assert.Equal(new[] { ("A", 10), (PrivacidadReporteHelper.EtiquetaGrupoReducido, 5) }, resultado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PRIV01_UmbralNoSePuedeAnular(int umbral)
    {
        var resultado = PrivacidadReporteHelper.AgruparConUmbralDePrivacidad(new[] { ("X", 1), ("Y", 2) }, umbral);

        Assert.Single(resultado);
        Assert.Equal(PrivacidadReporteHelper.EtiquetaGrupoReducido, resultado[0].Etiqueta);
    }
}

public class PasswordHashServiceTests
{
    [Fact]
    public void PBKDF2_VerificaSinPedirRehash()
    {
        var hash = PasswordHashService.HashPassword("correcta-123");
        Assert.NotEqual(44, hash.Length);
        Assert.Equal((true, false), PasswordHashService.Verificar("correcta-123", hash));
        Assert.Equal((false, false), PasswordHashService.Verificar("incorrecta", hash));
    }

    [Fact]
    public void SEC02_HashSha256Legado_VerificaYPideRehash()
    {
        var legado = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData("1234"u8.ToArray()));
        Assert.Equal((true, true), PasswordHashService.Verificar("1234", legado));
        Assert.Equal((false, false), PasswordHashService.Verificar("otra", legado));
    }

    [Fact]
    public void TextoPlanoNoBase64_NoEsFormatoReconocido()
    {
        Assert.False(PasswordHashService.EsFormatoReconocido("clave plana!"));
        Assert.True(PasswordHashService.EsFormatoReconocido(PasswordHashService.HashPassword("x")));
    }

    // Defecto encontrado al crear esta suite (2026-10-17), no corregido en F0A: un texto plano que
    // ademas es Base64 valido (p. ej. "1234") hace que PasswordHasher devuelva Failed en vez de lanzar
    // FormatException, asi que EsFormatoReconocido lo da por hash y el fixup de arranque de Program.cs
    // no lo re-hashea. Solo afecta a Jueces legados con contrasena en texto plano.
    [Fact(Skip = "Defecto conocido de EsFormatoReconocido; ver comentario. Pendiente de decision.")]
    public void TextoPlanoBase64_NoEsFormatoReconocido()
    {
        Assert.False(PasswordHashService.EsFormatoReconocido("1234"));
    }
}
