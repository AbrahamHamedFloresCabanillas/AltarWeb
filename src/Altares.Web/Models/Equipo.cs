using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AltarWeb.Models
{
    public class Equipo
    {
        public int Id { get; set; }

        [Required]
        public string NombreEquipo { get; set; } = string.Empty;

        public string PeriodoAcademico { get; set; } = string.Empty;

        public int? CreadoPorAlumnoId { get; set; }
        public virtual Alumno? CreadoPorAlumno { get; set; }

        public string SnapshotNombreCreador { get; set; } = string.Empty;

        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public virtual ICollection<AlumnoEquipo> Integrantes { get; set; } = new List<AlumnoEquipo>();
        public virtual ICollection<Evaluacion> Evaluaciones { get; set; } = new List<Evaluacion>();

        [NotMapped]
        public bool EsHistorico => PeriodoAcademico != PeriodoHelper.ObtenerPeriodoActual();
    }
}
