namespace AltarWeb.Models.Registro
{
    public class EvaluacionCatrina
    {
        public int Id { get; set; }

        public int EvaluacionId { get; set; }
        public virtual Evaluacion Evaluacion { get; set; } = null!;

        public decimal SombreroTocado { get; set; }
        public decimal Guantes { get; set; }
        public decimal Vestimenta { get; set; }
        public decimal Zapatos { get; set; }
        public decimal Collar { get; set; }
        public decimal Maquillaje { get; set; }

        public decimal? NotaCatrina { get; set; }
        public int? LugarCatrina { get; set; }
    }
}
