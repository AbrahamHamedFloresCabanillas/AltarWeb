namespace AltarWeb.Models
{
    public class AlumnoEquipo
    {
        public int AlumnoId { get; set; }
        public virtual Alumno Alumno { get; set; } = null!;

        public int EquipoId { get; set; }
        public virtual Equipo Equipo { get; set; } = null!;

        public DateTime FechaIngreso { get; set; } = DateTime.Now;
        public bool EsCreador { get; set; } = false;
    }
}
