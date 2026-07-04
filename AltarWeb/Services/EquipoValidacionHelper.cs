using AltarWeb.Models.Registro;

namespace AltarWeb.Services
{
    // Reglas de negocio compartidas para validar el estado de un Equipo antes de acciones criticas.
    // Antes vivian duplicadas (RegistroController tenia su propia copia privada de EsFichaCompleta);
    // ahora es la unica fuente para que RegistroController (dashboard) y AltarEvaluacionController
    // (SEC-13: cierre de evaluacion Final) no puedan divergir.
    public static class EquipoValidacionHelper
    {
        public static bool EsFichaCompleta(Equipo equipo)
        {
            return !string.IsNullOrWhiteSpace(equipo.NombreAltar)
                && !string.IsNullOrWhiteSpace(equipo.UbicacionAltar)
                && equipo.Difunto != null
                && !string.IsNullOrWhiteSpace(equipo.Difunto.Nombre)
                && equipo.MaestroEncargadoId != null;
        }

        // vision.md 4.2: el equipo debe tener exactamente un Narrador designado.
        public static bool TieneNarradorDesignado(Equipo equipo)
        {
            return equipo.Integrantes.Count(i => i.Rol == RolEquipo.Narrador) == 1;
        }
    }
}
