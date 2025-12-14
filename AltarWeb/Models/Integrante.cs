using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AltarWeb.Models
{
    public class Integrante
    {
        [Key]
        public int Id { get; set; }
        public int EvaluacionId { get; set; }

        public string Nombre { get; set; } = string.Empty;
        public string Matricula { get; set; } = string.Empty;

        public string Correo { get; set; } = string.Empty;

        [ForeignKey("EvaluacionId")]
        public virtual Evaluacion Evaluacion { get; set; } = null!;
    }
}