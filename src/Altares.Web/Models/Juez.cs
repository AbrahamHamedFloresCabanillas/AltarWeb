using System.ComponentModel.DataAnnotations;

namespace AltarWeb.Models
{
    public class Juez
    {
        [Key]
        public int Id { get; set; }

        // Nullable: las cuentas creadas por el admin desde /AltarAdmin/CrearJuez siguen
        // usando un nombre de usuario; las cuentas auto-registradas inician sesion con su
        // CorreoInstitucional y no capturan un usuario propio.
        public string? Usuario { get; set; }
        public string Password { get; set; } = string.Empty;

        // --- RBAC ---
        // Roles válidos: "Admin", "Juez", "Estudiante"
        public string Rol { get; set; } = "Juez";

        // Nombre completo para mostrar en reportes y historial
        public string NombreCompleto { get; set; } = string.Empty;

        // --- Auto-registro (jueces) ---
        public string? CorreoInstitucional { get; set; }
        public string? Identificador { get; set; }
        // true mientras el admin no lo apruebe desde /AltarAdmin/Jueces; bloquea el login.
        public bool Pendiente { get; set; }
        public string ProveedorAuth { get; set; } = "Local";

        // SEC-03: fuerza el cambio de contraseña antes de permitir cualquier otra accion
        // (cuenta admin semilla u otras marcadas explicitamente).
        public bool DebeCambiarPassword { get; set; } = false;

        // --- Soft Delete ---
        public bool IsDeleted { get; set; } = false;
        public DateTime? FechaEliminado { get; set; }
    }
}