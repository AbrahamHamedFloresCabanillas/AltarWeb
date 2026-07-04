using System.ComponentModel.DataAnnotations;

namespace AltarWeb.Models.Registro
{
    public class Difunto
    {
        public int Id { get; set; }

        public int EquipoId { get; set; }
        public virtual Equipo Equipo { get; set; } = null!;

        [Required]
        public string Nombre { get; set; } = string.Empty;

        public DateOnly FechaDefuncion { get; set; }

        public string? SemblanzaHobbiesTematica { get; set; }

        public TipoAltar TipoAltar { get; set; }
    }
}
