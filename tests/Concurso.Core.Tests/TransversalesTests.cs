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

    // Regresion del defecto encontrado en F0A (2026-10-17): un texto plano que ademas es Base64 valido
    // (p. ej. "1234", "abcd", 8 o 12 caracteres alfanumericos) hacia que PasswordHasher devolviera
    // Failed en vez de lanzar FormatException, asi que EsFormatoReconocido lo daba por hash y el fixup de
    // arranque de Program.cs no lo re-hasheaba: la contrasena quedaba en texto plano en la BD.
    [Theory]
    [InlineData("1234")]
    [InlineData("abcd")]
    [InlineData("password")]
    [InlineData("admin2026abc")]
    public void TextoPlanoQueEsBase64Valido_NoEsFormatoReconocido(string textoPlano)
    {
        Assert.False(PasswordHashService.EsFormatoReconocido(textoPlano));
    }

    // Un texto plano de 44 caracteres que no decodifica a 32 bytes tampoco es un SHA-256 legado.
    [Fact]
    public void TextoPlanoDe44Caracteres_NoEsHashLegado()
    {
        Assert.False(PasswordHashService.EsFormatoReconocido(new string('x', 43) + "!"));
    }

    // Los formatos reales siguen reconocidos: si el fix los rechazara, el fixup de arranque
    // re-hashearia un hash como si fuera texto plano y romperia esas cuentas.
    [Fact]
    public void FormatosRealesSiguenReconocidos()
    {
        var v3 = PasswordHashService.HashPassword("x");
        var v2 = new Microsoft.AspNetCore.Identity.PasswordHasher<object>(
            Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Identity.PasswordHasherOptions
            {
                CompatibilityMode = Microsoft.AspNetCore.Identity.PasswordHasherCompatibilityMode.IdentityV2
            })).HashPassword(new object(), "x");
        var sha256Legado = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData("x"u8.ToArray()));

        Assert.True(PasswordHashService.EsFormatoReconocido(v3));
        Assert.True(PasswordHashService.EsFormatoReconocido(v2));
        Assert.True(PasswordHashService.EsFormatoReconocido(sha256Legado));
    }
}
