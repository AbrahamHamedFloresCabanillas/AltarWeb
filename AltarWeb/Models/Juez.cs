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
    }
}