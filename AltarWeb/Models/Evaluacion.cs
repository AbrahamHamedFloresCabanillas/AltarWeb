using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AltarWeb.Models
{
    public class Evaluacion
    {
        [Key]
        public int Id { get; set; }
        public int JuezId { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "El nombre del equipo es obligatorio")]
        public string NombreEquipo { get; set; } = string.Empty; // Inicializado

        [Required(ErrorMessage = "El nombre del difunto es obligatorio")]
        public string NombreDifunto { get; set; } = string.Empty; // Inicializado

        public string Niveles { get; set; } = string.Empty; // Inicializado
        public string TipoAltar { get; set; } = string.Empty; // Inicializado

        [Required(ErrorMessage = "Los hobbies son obligatorios")]
        public string Hobbies { get; set; } = string.Empty; // Inicializado

        // Calificaciones
        public decimal NotaTradicion { get; set; }
        public decimal NotaPersonalizacion { get; set; }
        public decimal NotaEstetica { get; set; }
        public decimal NotaFinal { get; set; }

        // Checkboxes
        public bool ChkFoto { get; set; }
        public bool ChkVelas { get; set; }
        public bool ChkFlor { get; set; }
        public bool ChkPapel { get; set; }
        public bool ChkPan { get; set; }
        public bool ChkAgua { get; set; }
        public bool ChkSal { get; set; }
        public bool ChkIncienso { get; set; }
        public bool ChkCalaveritas { get; set; }
        public bool ChkObjetos { get; set; }

        public int BonusTematicos { get; set; }

        // Relaciones (Usamos null! para decirle al compilador que EF se encarga)
        [ForeignKey("JuezId")]
        public virtual Juez Juez { get; set; } = null!;
        public virtual List<Integrante> Integrantes { get; set; } = new List<Integrante>();
    }
}