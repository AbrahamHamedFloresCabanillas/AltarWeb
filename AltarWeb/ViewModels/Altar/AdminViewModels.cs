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

    // SEC-12: CrearJuez bindeaba la entidad Juez completa (over-posting: Id, IsDeleted, FechaEliminado,
    // Pendiente, CorreoInstitucional, ProveedorAuth quedaban expuestos al binder). Solo estos 4 campos
    // son capturables desde el formulario; el resto se fija explicitamente en el controlador.
    public class CrearJuezViewModel
    {
        [Required(ErrorMessage = "Ingresa el nombre completo.")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa un usuario.")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa una contraseña.")]
        public string Password { get; set; } = string.Empty;

        public string Rol { get; set; } = "Juez";
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

        // PRIV-01: piso minimo de 3 (vision.md 12.2 recomienda 5 por defecto) para que el umbral no
        // pueda anularse a 0/negativo y exponer categorias demograficas de 1-2 personas identificables.
        [Range(3, 100, ErrorMessage = "El umbral debe estar entre 3 y 100 para proteger datos demográficos sensibles.")]
        public int UmbralAgrupacionDemografica { get; set; } = 5;

        public int CarrerasCount { get; set; }
        public int GenerosCount { get; set; }
        public int ElementosCount { get; set; }
    }
}
