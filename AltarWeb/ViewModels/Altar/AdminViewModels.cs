using System.ComponentModel.DataAnnotations;
using AltarWeb.Models.Registro;

namespace AltarWeb.ViewModels.Altar
{
    public class RegistranteAdminRowViewModel
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string CorreoInstitucional { get; set; } = string.Empty;
        public TipoRegistrante Tipo { get; set; }
        public string Identificador { get; set; } = string.Empty;
        public string? Carrera { get; set; }
        public string? Genero { get; set; }
        public bool Activo { get; set; }
    }

    public class EquipoAdminCardViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string NombreAltar { get; set; } = string.Empty;
        public string Carrera { get; set; } = string.Empty;
        public int IntegrantesCount { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public string? MaestroEncargado { get; set; }
        public string? UbicacionAltar { get; set; }
        public bool Evaluado { get; set; }
        public bool FichaCompleta { get; set; }
    }

    public class ConfiguracionPeriodoAdminViewModel
    {
        public string Periodo { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? FechaLimiteInscripcion { get; set; }

        [DataType(DataType.Date)]
        public DateTime? FechaLimiteRequisitos { get; set; }

        public string? RecorridoPdf { get; set; }

        [Range(0, 100)] public decimal PesoObjetivoCulturalPct { get; set; }
        [Range(0, 100)] public decimal PesoEsenciaPersonalidadPct { get; set; }
        [Range(0, 100)] public decimal PesoValoracionGeneralPct { get; set; }
        [Range(0, 100)] public decimal PesoDistribucionNivelesPct { get; set; }
        [Range(0, 100)] public decimal PesoNarradorPct { get; set; }

        public decimal SumaPesos => PesoObjetivoCulturalPct + PesoEsenciaPersonalidadPct + PesoValoracionGeneralPct + PesoDistribucionNivelesPct + PesoNarradorPct;

        public int CarrerasCount { get; set; }
        public int GenerosCount { get; set; }
        public int ElementosCount { get; set; }
    }
}
