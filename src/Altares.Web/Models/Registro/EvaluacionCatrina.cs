namespace AltarWeb.Models.Registro
{
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
