namespace AltarWeb.Models.Registro
{
    public class RegistranteEquipo
    {
        public int RegistranteId { get; set; }
        public virtual Registrante Registrante { get; set; } = null!;

        public int EquipoId { get; set; }
        public virtual Equipo Equipo { get; set; } = null!;

        public RolEquipo Rol { get; set; } = RolEquipo.Integrante;
        public DateTime FechaIngreso { get; set; } = DateTime.Now;
    }
}
