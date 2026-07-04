using System.ComponentModel.DataAnnotations;

namespace AltarWeb.Models.Registro
{
    public class Elemento
    {
        public int Id { get; set; }

        [Required]
        public string Nombre { get; set; } = string.Empty;

        public CategoriaElemento Categoria { get; set; }

        // Niveles sugeridos: vision.md limita Niveles a 3 o 7 (seccion 4.4/13.3), por eso son
        // dos columnas fijas en vez de una tabla ElementoNivel(ElementoId, TotalNiveles, Nivel).
        public int? NivelSugerido3 { get; set; }
        public int? NivelSugerido7 { get; set; }

        public string Significado { get; set; } = string.Empty;

        public string Colocacion { get; set; } = string.Empty;

        public int Orden { get; set; }

        public bool Activo { get; set; } = true;
    }
}
