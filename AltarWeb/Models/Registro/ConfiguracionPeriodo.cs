using System.ComponentModel.DataAnnotations;

namespace AltarWeb.Models.Registro
{
    public class ConfiguracionPeriodo
    {
        public int Id { get; set; }

        [Required]
        public string Periodo { get; set; } = string.Empty;

        public DateTime? FechaLimiteInscripcion { get; set; }
        public DateTime? FechaLimiteRequisitos { get; set; }

        public string? RecorridoPdf { get; set; }

        // Pesos de la formula de NotaFinal, suman 1.0 (seccion 7.7 de vision.md)
        public decimal PesoObjetivoCultural { get; set; } = 0.30m;
        public decimal PesoEsenciaPersonalidad { get; set; } = 0.30m;
        public decimal PesoValoracionGeneral { get; set; } = 0.20m;
        public decimal PesoDistribucionNiveles { get; set; } = 0.10m;
        public decimal PesoNarrador { get; set; } = 0.10m;

        // Escala de satisfaccion por elemento (seccion 7.2)
        public decimal ValorSatisfaccionNoPresente { get; set; } = 0.0m;
        public decimal ValorSatisfaccionPoco { get; set; } = 0.5m;
        public decimal ValorSatisfaccionSatisfactorio { get; set; } = 0.75m;
        public decimal ValorSatisfaccionMuySatisfactorio { get; set; } = 1.0m;

        // Peso relativo de elementos rituales vs decorativos (seccion 7.2)
        public decimal PesoElementoRitual { get; set; } = 1.0m;
        public decimal PesoElementoDecorativo { get; set; } = 0.5m;

        // Bonus tematico por elemento marcado como "tiene personalizacion" (equivalente al
        // BonusTematicos del sistema anterior); se suma a EsenciaPersonalidad, tope 10.
        public decimal BonusPorElementoTematizado { get; set; } = 0.25m;

        // Si esta activo, Difunto.FechaDefuncion debe ser al menos 1 año anterior a hoy.
        public bool ExigirMinimoUnAnioFallecimiento { get; set; } = true;
    }
}
