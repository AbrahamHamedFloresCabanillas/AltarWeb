namespace Concurso.Core.Fechas
{
    public static class PeriodoHelper
    {
        public static string ObtenerPeriodoActual(DateTime? fecha = null)
        {
            var valor = fecha ?? FechaConcursoHelper.AhoraLocal;
            return valor.Month <= 7 ? $"{valor.Year}-1" : $"{valor.Year}-2";
        }

        public static bool EsPeriodoActual(string periodo)
        {
            return periodo == ObtenerPeriodoActual();
        }
    }
}
