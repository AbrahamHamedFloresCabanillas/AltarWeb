using AltarWeb.Models;

namespace AltarWeb.Models.Registro
{
    public class Evaluacion
    {
        public int Id { get; set; }

        public int EquipoId { get; set; }
        public virtual Equipo Equipo { get; set; } = null!;

        public int? JuezId { get; set; }
        public virtual Juez? Juez { get; set; }

        public string Periodo { get; set; } = string.Empty;

        public int Niveles { get; set; }

        public EstadoEvaluacion Estado { get; set; } = EstadoEvaluacion.Preliminar;

        // Componentes calificables (seccion 7 de vision.md)
        public decimal PuntajeElementos { get; set; }
        public decimal DistribucionNiveles { get; set; }
        public decimal Narrador { get; set; }
        public decimal TematicaHobbies { get; set; }

        // ObjetivoCultural ya no lo captura el juez a mano: se sincroniza siempre con
        // PuntajeElementos (decision del comite, sustituye el slider manual).
        public decimal ObjetivoCultural { get; set; }

        // EsenciaPersonalidad es la nota cruda que captura el juez (slider). El bonus por
        // elementos "tematizados" (ver README, BonusTematicos del sistema anterior) se suma
        // aparte en EsenciaPersonalidadFinal para no duplicarse en guardados repetidos de
        // una evaluacion Preliminar.
        public decimal EsenciaPersonalidad { get; set; }
        public decimal? EsenciaPersonalidadFinal { get; set; }

        public decimal ValoracionGeneral { get; set; }

        public bool IncluyeCatrina { get; set; }
        public virtual EvaluacionCatrina? EvaluacionCatrina { get; set; }

        public decimal? NotaFinal { get; set; }
        public int? Lugar { get; set; }

        // Snapshot capturado al momento de evaluar (seccion 4.4)
        public string SnapshotNombreEquipo { get; set; } = string.Empty;
        public string SnapshotNombreAltar { get; set; } = string.Empty;
        public string SnapshotDifuntoNombre { get; set; } = string.Empty;
        public DateOnly? SnapshotDifuntoFechaDefuncion { get; set; }

        public DateTime CreadoEn { get; set; } = DateTime.Now;
        public DateTime? ActualizadoEn { get; set; }

        public virtual ICollection<EvaluacionIntegrante> Integrantes { get; set; } = new List<EvaluacionIntegrante>();
        public virtual ICollection<ElementoEvaluado> ElementosEvaluados { get; set; } = new List<ElementoEvaluado>();
    }
}
