namespace AltarWeb.Models.Registro
{
    // A1 (pruebas adversariales 2026-10-21): el organizador ya no agrega integrantes directamente; los invita y la
    // persona acepta o rechaza desde su portal. Mientras no acepte no es integrante ni queda atada al equipo.
    public class InvitacionEquipo
    {
        public int Id { get; set; }

        public int EquipoId { get; set; }
        public virtual Equipo Equipo { get; set; } = null!;

        public int RegistranteId { get; set; }
        public virtual Registrante Registrante { get; set; } = null!;

        // Copia de Equipo.Periodo, como RegistranteEquipo.Periodo: al aceptar, la persona ya tiene equipo en este
        // periodo y sus demas invitaciones del periodo se descartan.
        public string Periodo { get; set; } = string.Empty;

        public DateTime CreadaEn { get; set; } = DateTime.Now;
    }
}
