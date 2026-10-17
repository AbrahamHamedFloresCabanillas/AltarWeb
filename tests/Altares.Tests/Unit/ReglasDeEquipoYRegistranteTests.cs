using System.ComponentModel.DataAnnotations;
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
