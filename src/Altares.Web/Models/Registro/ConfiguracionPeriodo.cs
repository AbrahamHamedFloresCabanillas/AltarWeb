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

        // Escala de satisfaccion por elemento (seccion 7.2): No lo tiene / Regular / Bueno.
        // "Bueno" debe valer 1.0 para que el Puntaje de Elementos pueda llegar a 10.
        public decimal ValorSatisfaccionNoLoTiene { get; set; } = 0.0m;
        public decimal ValorSatisfaccionRegular { get; set; } = 0.5m;
        public decimal ValorSatisfaccionBueno { get; set; } = 1.0m;

        // Peso relativo de elementos rituales vs decorativos (seccion 7.2)
        public decimal PesoElementoRitual { get; set; } = 1.0m;
        public decimal PesoElementoDecorativo { get; set; } = 0.5m;

        // Bonus tematico por elemento marcado como "tematizado" (equivalente al
        // BonusTematicos del sistema anterior); se suma a EsenciaPersonalidad, tope 10.
        public decimal BonusPorElementoTematizado { get; set; } = 0.25m;

        // Si esta activo, Difunto.FechaDefuncion debe ser al menos 1 año anterior a hoy.
        public bool ExigirMinimoUnAnioFallecimiento { get; set; } = true;

        // Umbral de agrupacion de privacidad para el Reporte de Cierre de Periodo (seccion
        // 12.2 de vision.md): cualquier categoria de Genero/AutodescripcionCultural con
        // menos personas que este numero se agrupa bajo "Otros / grupo reducido".
        public int UmbralAgrupacionDemografica { get; set; } = 5;

        // LEGADO (F2, 2026-10-17): el Concurso de Catrinas se separo en Catrinas.Web. Altares ya no lee ni escribe
        // ni muestra estos pesos; las columnas se conservan fisicamente hasta despues del concurso.
        public decimal PesoCatrinaVestuario { get; set; } = 0.25m;
        public decimal PesoCatrinaMaquillaje { get; set; } = 0.20m;
        public decimal PesoCatrinaAccesorios { get; set; } = 0.10m;
        public decimal PesoCatrinaOriginalidadCreatividad { get; set; } = 0.20m;
        public decimal PesoCatrinaApegoTradiciones { get; set; } = 0.25m;
    }
}
