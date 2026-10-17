namespace AltarWeb.ViewModels.Altar
{
    // Estado global de avance de una carrera en el recorrido del concurso.
    public enum AvanceEstado
    {
        SinCalificar,
        Preliminar,
        Final
    }

    public class AvanceCarreraViewModel
    {
        public string Carrera { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Finales { get; set; }
        public int Preliminares { get; set; }

        // Equipos de la carrera sin ninguna evaluacion todavia.
        public int SinCalificar => Total - Finales - Preliminares;

        // Bucket de clasificacion: Final solo cuando todos los equipos estan cerrados como Final;
        // Preliminar cuando ya empezo pero falta cerrar alguno; SinCalificar si nadie fue evaluado.
        public AvanceEstado Estado =>
            Total > 0 && Finales == Total ? AvanceEstado.Final
            : (Preliminares + Finales) > 0 ? AvanceEstado.Preliminar
            : AvanceEstado.SinCalificar;
    }
}
