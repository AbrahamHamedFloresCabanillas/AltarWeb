using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AltarWeb.Models
{
    public class Evaluacion
    {
        [Key]
        public int Id { get; set; }

        public int? JuezId { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Periodo { get; set; } = string.Empty;
        public string NombreJuez { get; set; } = string.Empty;

        public int? EquipoId { get; set; }
        public string SnapshotNombreEquipo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre del equipo es obligatorio")]
        public string NombreEquipo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre del difunto es obligatorio")]
        public string NombreDifunto { get; set; } = string.Empty;

        public string Niveles { get; set; } = string.Empty;
        public string TipoAltar { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los hobbies son obligatorios")]
        public string Hobbies { get; set; } = string.Empty;

        public decimal NotaTradicion { get; set; }
        public decimal NotaPersonalizacion { get; set; }
        public decimal NotaEstetica { get; set; }
        public decimal NotaTradicionFinal { get; set; }
        public decimal NotaPersonalizacionFinal { get; set; }
        public decimal NotaFinal { get; set; }

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

        [ForeignKey("JuezId")]
        public virtual Juez? Juez { get; set; }

        [ForeignKey("EquipoId")]
        public virtual Equipo? Equipo { get; set; }

        public virtual List<Integrante> Integrantes { get; set; } = new();

        public int ObtenerConteoElementos()
        {
            int enc = 0;
            if (ChkFoto) enc++;
            if (ChkVelas) enc++;
            if (ChkFlor) enc++;
            if (ChkPapel) enc++;
            if (ChkPan) enc++;
            if (ChkAgua) enc++;
            if (ChkSal) enc++;
            if (ChkIncienso) enc++;
            if (ChkCalaveritas) enc++;
            if (ChkObjetos) enc++;
            return enc;
        }

        public void CalcularNotasFinales()
        {
            NotaTradicionFinal = Math.Min(10, (ObtenerConteoElementos() + NotaTradicion) / 2);
            NotaPersonalizacionFinal = Math.Min(10, NotaPersonalizacion + (BonusTematicos * 0.5m));
            NotaFinal = (NotaTradicionFinal * 0.3m) + (NotaPersonalizacionFinal * 0.4m) + (NotaEstetica * 0.3m);
        }

        public string ObtenerNombreEquipo()
        {
            if (!string.IsNullOrWhiteSpace(SnapshotNombreEquipo)) return SnapshotNombreEquipo;
            if (!string.IsNullOrWhiteSpace(NombreEquipo)) return NombreEquipo;
            return Equipo?.NombreEquipo ?? string.Empty;
        }
    }
}
