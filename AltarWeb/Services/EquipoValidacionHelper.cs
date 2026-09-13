using AltarWeb.Models.Registro;

namespace AltarWeb.Services
{
    // Reglas de negocio compartidas para validar el estado de un Equipo antes de acciones criticas.
    // Antes vivian duplicadas (RegistroController tenia su propia copia privada de EsFichaCompleta);
    // ahora es la unica fuente para que RegistroController (dashboard), AltarEvaluacionController
    // (SEC-13: cierre de evaluacion Final), AltarAdminController (listado de equipos) y
    // ReportePeriodoService no puedan divergir.
    //
    // Hay TRES conceptos distintos que no deben mezclarse:
    //   1. EsFichaCompleta      -> objetivamente estan capturados todos los datos requeridos.
    //   2. FueValidadaPorAdmin  -> un administrador la reviso y la acepto para seguimiento.
    //   3. EstaListoParaFinal   -> se puede cerrar la evaluacion como Final.
    // (2) NO relaja (1) ni (3): la validacion administrativa silencia el aviso de seguimiento, pero
    // el snapshot de la evaluacion y las constancias siguen necesitando los datos reales.
    public static class EquipoValidacionHelper
    {
        public static bool EsFichaCompleta(Equipo equipo) => FaltantesFicha(equipo).Count == 0;

        // Lista legible de lo que falta, para poder decirle al usuario QUE falta en vez de solo
        // "pendiente". El orden es el mismo en el que aparecen los campos en la Ficha de Registro.
        public static IReadOnlyList<string> FaltantesFicha(Equipo equipo)
        {
            var faltantes = new List<string>();

            if (string.IsNullOrWhiteSpace(equipo.NombreAltar)) faltantes.Add("nombre del altar");
            if (equipo.Difunto == null || string.IsNullOrWhiteSpace(equipo.Difunto.Nombre)) faltantes.Add("nombre del difunto");
            if (string.IsNullOrWhiteSpace(equipo.UbicacionAltar)) faltantes.Add("lugar de exposición");
            if (equipo.MaestroEncargadoId == null) faltantes.Add("maestro encargado vinculado");

            return faltantes;
        }

        // vision.md 4.2: el equipo debe tener exactamente un Narrador designado.
        public static bool TieneNarradorDesignado(Equipo equipo)
        {
            return equipo.Integrantes.Count(i => i.Rol == RolEquipo.Narrador) == 1;
        }

        public static bool FueValidadaPorAdmin(Equipo equipo) => equipo.FichaValidadaPorAdminEn != null;

        // SEC-13: regla unica para cerrar una evaluacion como Final. Devuelve el motivo del rechazo
        // para que el mensaje al juez explique que falta, en vez de un "no permitido" generico.
        public static bool EstaListoParaFinal(Equipo equipo, out string? motivo)
        {
            if (!TieneNarradorDesignado(equipo))
            {
                motivo = "el equipo no tiene un Narrador designado";
                return false;
            }

            var faltantes = FaltantesFicha(equipo);
            if (faltantes.Count > 0)
            {
                motivo = $"la Ficha de Registro está incompleta (falta: {string.Join(", ", faltantes)})";
                return false;
            }

            motivo = null;
            return true;
        }
    }
}
