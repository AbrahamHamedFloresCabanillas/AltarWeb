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

        // SEC-11: copia denormalizada de Equipo.Periodo al momento de unirse, para que la BD pueda
        // imponer "un registrante por equipo activo por periodo" con un indice unico (RegistranteId,
        // Periodo), sin depender solo del check-then-insert de la aplicacion (TOCTOU).
        public string Periodo { get; set; } = string.Empty;
    }
}
