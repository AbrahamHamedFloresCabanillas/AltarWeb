using System.ComponentModel.DataAnnotations;

namespace AltarWeb.Models
{
    public class Juez
    {
        [Key]
        public int Id { get; set; }

        // Inicializamos con string.Empty para evitar el error de nulos
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // --- RBAC ---
        // Roles válidos: "Admin", "Juez", "Estudiante"
        public string Rol { get; set; } = "Juez";

        // Nombre completo para mostrar en reportes y historial
        public string NombreCompleto { get; set; } = string.Empty;

        // --- Soft Delete ---
        public bool IsDeleted { get; set; } = false;
        public DateTime? FechaEliminado { get; set; }
    }
}