using System.ComponentModel.DataAnnotations;
using AltarWeb.Models.Registro;

namespace AltarWeb.ViewModels.Altar
{
    public class CrearEquipoViewModel
    {
        [Required(ErrorMessage = "Ingresa el nombre del grupo.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el nombre del altar.")]
        public string NombreAltar { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona la carrera del altar.")]
        public int CarreraId { get; set; }

        [Required(ErrorMessage = "Ingresa la matrícula del maestro encargado.")]
        public string MaestroEncargadoIdentificador { get; set; } = string.Empty;

        public string? UbicacionAltar { get; set; }

        [Required(ErrorMessage = "Ingresa el nombre del difunto.")]
        public string DifuntoNombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa la fecha de defunción.")]
        [DataType(DataType.Date)]
        public DateOnly DifuntoFechaDefuncion { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(-1));

        public bool HaceCatrina { get; set; }

        public List<CatalogoCarrera> Carreras { get; set; } = new();
    }

    public class FichaViewModel
    {
        public int EquipoId { get; set; }
        public int? CreadoPorRegistranteId { get; set; }

        public string ResponsableNombre { get; set; } = string.Empty;
        public string? ResponsableTelefono { get; set; }
        public string ResponsableCorreo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el nombre del grupo.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el nombre del altar.")]
        public string NombreAltar { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el nombre del difunto.")]
        public string DifuntoNombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa la fecha de defunción.")]
        [DataType(DataType.Date)]
        public DateOnly DifuntoFechaDefuncion { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(-1));

        [Required(ErrorMessage = "Selecciona la carrera del altar.")]
        public int CarreraId { get; set; }

        [Required(ErrorMessage = "Ingresa la matrícula del maestro encargado.")]
        public string MaestroEncargadoIdentificador { get; set; } = string.Empty;

        public string? UbicacionAltar { get; set; }

        public bool HaceCatrina { get; set; }

        public DateTime CreadoEn { get; set; }

        public List<CatalogoCarrera> Carreras { get; set; } = new();
        public List<RegistranteEquipo> Integrantes { get; set; } = new();

        // Info de maestro encargado para mostrarlo como "integrante" en la vista.
        public string? MaestroEncargadoNombre { get; set; }
        public bool MaestroEncargadoPendiente { get; set; }

        public bool PuedeEditar { get; set; }
        public bool ExigirMinimoUnAnioFallecimiento { get; set; }
        public bool FechaLimiteRequisitosPasada { get; set; }
    }
}
