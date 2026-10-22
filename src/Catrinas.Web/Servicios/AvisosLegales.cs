namespace Catrinas.Web
{
    // Avisos legales del Concurso de Catrinas (peticion del propietario, 2026-10-22). Version que se guarda junto con
    // la aceptacion de cada cuenta: si el aviso o los terminos cambian de fondo, se sube la version.
    //
    // Los datos de contacto del responsable se leen de la configuracion del sitio (variables de entorno en el
    // hosting): Legal__CorreoPrivacidad, Legal__Domicilio, Legal__PlazoConservacion. Sin configurar se muestra
    // "[POR DEFINIR]": hay que completarlos antes de cargar datos reales (gate ADR §8.4).
    public static class AvisosLegales
    {
        public const string Version = "2026-10-22";
        public const string PorDefinir = "[POR DEFINIR]";

        public static string Dato(IConfiguration configuracion, string clave) =>
            string.IsNullOrWhiteSpace(configuracion[$"Legal:{clave}"]) ? PorDefinir : configuracion[$"Legal:{clave}"]!.Trim();
    }
}
