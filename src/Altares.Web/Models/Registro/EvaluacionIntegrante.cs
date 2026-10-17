namespace AltarWeb.Models.Registro
{
    public class EvaluacionIntegrante
    {
        public int Id { get; set; }

        public int EvaluacionId { get; set; }
        public virtual Evaluacion Evaluacion { get; set; } = null!;

        public int? RegistranteId { get; set; }
        public virtual Registrante? Registrante { get; set; }

        public string NombreCompleto { get; set; } = string.Empty;
        public string Identificador { get; set; } = string.Empty;
        public RolEquipo Rol { get; set; }
    }
}
