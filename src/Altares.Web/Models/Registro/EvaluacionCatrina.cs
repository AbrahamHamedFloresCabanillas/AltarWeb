namespace AltarWeb.Models.Registro
{
    // LEGADO (F2, 2026-10-17): el Concurso de Catrinas se separo en Catrinas.Web. Altares ya no lee ni escribe
    // esta tabla (EvaluacionesCatrina); se conserva fisicamente con sus datos hasta despues del concurso.
    public class EvaluacionCatrina
    {
        public int Id { get; set; }

        public int EvaluacionId { get; set; }
        public virtual Evaluacion Evaluacion { get; set; } = null!;

        public decimal Vestuario { get; set; }
        public decimal Maquillaje { get; set; }
        public decimal Accesorios { get; set; }
        public decimal OriginalidadCreatividad { get; set; }
        public decimal ApegoTradiciones { get; set; }

        public decimal? NotaCatrina { get; set; }
        public int? LugarCatrina { get; set; }
    }
}
