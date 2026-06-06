using System.ComponentModel.DataAnnotations;

namespace AltarWeb.Models
{
    public class Alumno
    {
        public int Id { get; set; }

        [Required]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required]
        public string Matricula { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string CorreoElectronico { get; set; } = string.Empty;

        public string? PasswordHash { get; set; }

        public string ProveedorAuth { get; set; } = "Local";

        public bool Activo { get; set; } = true;
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
        public string PeriodoRegistro { get; set; } = string.Empty;

        public virtual ICollection<AlumnoEquipo> Equipos { get; set; } = new List<AlumnoEquipo>();
    }
}
