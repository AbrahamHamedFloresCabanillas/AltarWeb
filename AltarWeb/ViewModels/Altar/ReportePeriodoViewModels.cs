namespace AltarWeb.ViewModels.Altar
{
    public class ReportePeriodoViewModel
    {
        public string Periodo { get; set; } = string.Empty;
        public List<string> PeriodosDisponibles { get; set; } = new();
        public int UmbralAgrupacionDemografica { get; set; }
        public DateTime GeneradoEn { get; set; } = DateTime.Now;

        public ParticipacionGeneralViewModel ParticipacionGeneral { get; set; } = new();
        public DistribucionAcademicaViewModel DistribucionAcademica { get; set; } = new();
        public ResultadosEvaluacionViewModel ResultadosEvaluacion { get; set; } = new();
        public ParticipacionJuecesViewModel ParticipacionJueces { get; set; } = new();
        public EstadisticasDemograficasViewModel EstadisticasDemograficas { get; set; } = new();
    }

    public class ParticipacionGeneralViewModel
    {
        public int TotalEquipos { get; set; }
        public int EquiposFichaCompleta { get; set; }
        public int EquiposFichaIncompleta { get; set; }
        public List<(string Tipo, int Conteo)> RegistrantesPorTipo { get; set; } = new();
        public int EvaluacionesPreliminar { get; set; }
        public int EvaluacionesFinal { get; set; }
        public decimal PromedioIntegrantesPorEquipo { get; set; }
    }

    public class ConteoConPorcentaje
    {
        public string Etiqueta { get; set; } = string.Empty;
        public int Conteo { get; set; }
        public decimal Porcentaje { get; set; }
    }

    public class DistribucionAcademicaViewModel
    {
        public List<ConteoConPorcentaje> EquiposPorCarrera { get; set; } = new();
        public List<ConteoConPorcentaje> RegistrantesAlumnosPorCarrera { get; set; } = new();
        public List<(string Etiqueta, int Conteo)> EquiposPorTipoAltar { get; set; } = new();
        public List<(string Etiqueta, int Conteo)> EquiposPorNiveles { get; set; } = new();
        public int EquiposConCatrina { get; set; }
        public int EquiposSinCatrina { get; set; }
    }

    public class NotaStats
    {
        public decimal? Promedio { get; set; }
        public decimal? Maximo { get; set; }
        public decimal? Minimo { get; set; }
        public int Cantidad { get; set; }
    }

    public class PodiumEntry
    {
        public string Carrera { get; set; } = string.Empty;
        public int Lugar { get; set; }
        public string NombreEquipo { get; set; } = string.Empty;
        public string NombreAltar { get; set; } = string.Empty;
        public decimal? Nota { get; set; }
    }

    public class ResultadosEvaluacionViewModel
    {
        public NotaStats NotaFinalGeneral { get; set; } = new();
        public List<(string Carrera, NotaStats Stats)> NotaFinalPorCarrera { get; set; } = new();

        public decimal? PromedioObjetivoCultural { get; set; }
        public decimal? PromedioEsenciaPersonalidad { get; set; }
        public decimal? PromedioValoracionGeneral { get; set; }
        public decimal? PromedioDistribucionNiveles { get; set; }
        public decimal? PromedioNarrador { get; set; }
        public decimal? PromedioNotaCatrina { get; set; }
        public int EquiposConNotaCatrina { get; set; }

        public List<PodiumEntry> PodiumAltarPorCarrera { get; set; } = new();
        public List<PodiumEntry> PodiumCatrinaPorCarrera { get; set; } = new();

        public List<(string Elemento, int Conteo)> TopNoPresente { get; set; } = new();
        public List<(string Elemento, int Conteo)> TopMuySatisfactorio { get; set; } = new();
    }

    public class ParticipacionJuecesViewModel
    {
        public int TotalJuecesActivos { get; set; }
        public List<(string Juez, int Conteo)> EvaluacionesPorJuez { get; set; } = new();
        public int TotalMaestrosEncargadosDistintos { get; set; }
    }

    public class EstadisticasDemograficasViewModel
    {
        public int TotalRegistrantesParticipantes { get; set; }
        // Ya pasaron por PrivacidadReporteHelper.AgruparConUmbralDePrivacidad antes de llegar aqui.
        public List<(string Etiqueta, int Conteo)> Genero { get; set; } = new();
        public List<(string Etiqueta, int Conteo)> AutodescripcionCultural { get; set; } = new();
    }
}
