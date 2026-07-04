namespace AltarWeb.Models.Registro
{
    public class ElementoEvaluado
    {
        public int Id { get; set; }

        public int EvaluacionId { get; set; }
        public virtual Evaluacion Evaluacion { get; set; } = null!;

        public int ElementoId { get; set; }
        public virtual Elemento Elemento { get; set; } = null!;

        public Satisfaccion Satisfaccion { get; set; }

        // Nivel elegido manualmente por el juez para este elemento (independiente del
        // NivelSugerido3/7 del catalogo, que queda solo como referencia/mini-manual).
        public int? NivelAsignado { get; set; }

        // Bonus tematico (equivalente al "Bonus Temáticos" del sistema anterior, ver README.md).
        // Solo aplica si Satisfaccion != NoPresente.
        public bool Tematizado { get; set; }
    }
}
