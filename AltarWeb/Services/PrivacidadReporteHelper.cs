namespace AltarWeb.Services
{
    // Regla de proteccion de datos sensibles del Reporte de Cierre de Periodo
    // (vision.md seccion 12.2). Se usa exclusivamente en reporteria agregada
    // (ReportePeriodoService); las vistas administrativas de gestion individual
    // de registrantes (AltarAdminController) NO pasan por aqui y siguen mostrando
    // el dato real de una persona puntual.
    public static class PrivacidadReporteHelper
    {
        public const string EtiquetaGrupoReducido = "Otros / grupo reducido";

        // Agrupa bajo EtiquetaGrupoReducido cualquier categoria con menos personas que
        // "umbral" dentro del universo especifico recibido (el llamador ya debe haber
        // filtrado por el corte que se este mostrando: general, por carrera, etc.).
        public static List<(string Etiqueta, int Conteo)> AgruparConUmbralDePrivacidad(
            IEnumerable<(string Etiqueta, int Conteo)> categorias, int umbral)
        {
            var lista = categorias.ToList();

            var visibles = lista
                .Where(c => c.Conteo >= umbral)
                .OrderByDescending(c => c.Conteo)
                .ToList();

            var reducidos = lista
                .Where(c => c.Conteo < umbral)
                .Sum(c => c.Conteo);

            if (reducidos > 0)
            {
                visibles.Add((EtiquetaGrupoReducido, reducidos));
            }

            return visibles;
        }
    }
}
