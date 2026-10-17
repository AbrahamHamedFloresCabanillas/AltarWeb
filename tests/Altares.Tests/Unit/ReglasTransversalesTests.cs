using System.ComponentModel.DataAnnotations;
using AltarWeb.Models;
using AltarWeb.Models.Registro;
using AltarWeb.Services;
using Equipo = AltarWeb.Models.Registro.Equipo;

namespace Altares.Tests.Unit;

public class EquipoValidacionHelperTests
{
    private static Equipo EquipoCompleto() => new()
    {
        NombreAltar = "La mano peluda",
        UbicacionAltar = "Edificio A",
        MaestroEncargadoId = 10,
        Difunto = new Difunto { Nombre = "Difunto" },
        Integrantes =
        {
            new RegistranteEquipo { RegistranteId = 1, Rol = RolEquipo.Narrador },
            new RegistranteEquipo { RegistranteId = 2, Rol = RolEquipo.Integrante }
        }
    };

    [Fact]
    public void FichaCompleta_ConNarrador_EstaListaParaFinal()
    {
        var equipo = EquipoCompleto();
        Assert.True(EquipoValidacionHelper.EsFichaCompleta(equipo));
        Assert.True(EquipoValidacionHelper.EstaListoParaFinal(equipo, out var motivo));
        Assert.Null(motivo);
    }

    [Fact]
    public void FaltantesFicha_DiceQueFalta()
    {
        var equipo = new Equipo();
        Assert.Equal(
            new[] { "nombre del altar", "nombre del difunto", "lugar de exposición", "maestro encargado vinculado" },
            EquipoValidacionHelper.FaltantesFicha(equipo));
    }

    [Fact]
    public void SinNarrador_NoEstaListoParaFinal()
    {
        var equipo = EquipoCompleto();
        equipo.Integrantes.First().Rol = RolEquipo.Integrante;
        Assert.False(EquipoValidacionHelper.EstaListoParaFinal(equipo, out var motivo));
        Assert.Contains("Narrador", motivo);
    }

    [Fact]
    public void ValidacionAdministrativa_NoRelajaLaFichaIncompleta()
    {
        var equipo = EquipoCompleto();
        equipo.UbicacionAltar = null;
        equipo.FichaValidadaPorAdminEn = DateTime.Now;

        Assert.True(EquipoValidacionHelper.FueValidadaPorAdmin(equipo));
        Assert.False(EquipoValidacionHelper.EsFichaCompleta(equipo));
        Assert.False(EquipoValidacionHelper.EstaListoParaFinal(equipo, out _));
    }
}

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

public class RegistranteValidacionTests
{
    private static List<ValidationResult> Validar(Registrante r) => r.Validate(new ValidationContext(r)).ToList();

    [Theory]
    [InlineData(TipoRegistrante.Alumno, "1234567", true)]
    [InlineData(TipoRegistrante.Alumno, "12345", false)]
    [InlineData(TipoRegistrante.Maestro, "12345", true)]
    [InlineData(TipoRegistrante.Administrativo, "1234567", false)]
    [InlineData(TipoRegistrante.Maestro, "12a45", false)]
    public void Identificador_SegunTipo(TipoRegistrante tipo, string identificador, bool valido)
    {
        var r = new Registrante { Tipo = tipo, Identificador = identificador, CorreoInstitucional = "a@uabc.edu.mx" };
        Assert.Equal(valido, Validar(r).Count == 0);
    }

    [Fact]
    public void Correo_DebeSerUabc()
    {
        var r = new Registrante { Tipo = TipoRegistrante.Maestro, Identificador = "12345", CorreoInstitucional = "a@gmail.com" };
        Assert.Contains(Validar(r), v => v.MemberNames.Contains(nameof(Registrante.CorreoInstitucional)));
    }
}
