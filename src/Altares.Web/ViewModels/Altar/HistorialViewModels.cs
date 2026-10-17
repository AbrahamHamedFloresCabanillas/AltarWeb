using AltarWeb.Models.Registro;

namespace AltarWeb.ViewModels.Altar
{
    public class HistorialRowViewModel
    {
        public int EvaluacionId { get; set; }
        public string NombreEquipo { get; set; } = string.Empty;
        public TipoAltar TipoAltar { get; set; }
        public int Niveles { get; set; }
        public string Carrera { get; set; } = string.Empty;
        public string Difunto { get; set; } = string.Empty;
        public decimal? NotaFinal { get; set; }
        public int? Lugar { get; set; }
        public EstadoEvaluacion Estado { get; set; }
    }

    public class HistorialViewModel
    {
        public int TotalEvaluaciones { get; set; }
        public decimal? PromedioFinales { get; set; }
        public int CerradasFinal { get; set; }
        public int EquiposPendientes { get; set; }

        public Dictionary<string, List<HistorialRowViewModel>> Periodos { get; set; } = new();
    }

    public class EvaluacionDetalleViewModel
    {
        public int EvaluacionId { get; set; }
        public string NombreEquipo { get; set; } = string.Empty;
        public string NombreAltar { get; set; } = string.Empty;
        public TipoAltar TipoAltar { get; set; }
        public string Carrera { get; set; } = string.Empty;
        public string Difunto { get; set; } = string.Empty;
        public int Niveles { get; set; }
        public string Juez { get; set; } = string.Empty;

        public decimal ObjetivoCultural { get; set; }
        public decimal EsenciaPersonalidad { get; set; }
        public decimal ValoracionGeneral { get; set; }
        public decimal DistribucionNiveles { get; set; }
        public decimal Narrador { get; set; }
        public decimal? NotaFinal { get; set; }
        public int? Lugar { get; set; }

        public EstadoEvaluacion Estado { get; set; }

        public bool IncluyeCatrina { get; set; }
        public decimal? NotaCatrina { get; set; }
    }
}
