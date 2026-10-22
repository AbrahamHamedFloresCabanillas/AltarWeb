using System.ComponentModel.DataAnnotations;
using AltarWeb.Models.Registro;

namespace AltarWeb.ViewModels.Altar
{
    public class CrearEquipoViewModel
    {
        [Required(ErrorMessage = "Ingresa el nombre del grupo.")]
        [StringLength(LimitesTexto.NombreEquipo, ErrorMessage = LimitesTexto.Mensaje)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el nombre del altar.")]
        [StringLength(LimitesTexto.NombreAltar, ErrorMessage = LimitesTexto.Mensaje)]
        public string NombreAltar { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona la carrera del altar.")]
        public int CarreraId { get; set; }

        [Required(ErrorMessage = "Ingresa la matrícula del maestro encargado.")]
        public string MaestroEncargadoIdentificador { get; set; } = string.Empty;

        [StringLength(LimitesTexto.Ubicacion, ErrorMessage = LimitesTexto.Mensaje)]
        public string? UbicacionAltar { get; set; }

        [Required(ErrorMessage = "Ingresa el nombre del difunto.")]
        [StringLength(LimitesTexto.NombreDifunto, ErrorMessage = LimitesTexto.Mensaje)]
        public string DifuntoNombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa la fecha de defunción.")]
        [DataType(DataType.Date)]
        public DateOnly DifuntoFechaDefuncion { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(-1));


        public List<CatalogoCarrera> Carreras { get; set; } = new();
    }

    public class FichaViewModel
    {
        public int EquipoId { get; set; }
        public int? CreadoPorRegistranteId { get; set; }

        public string ResponsableNombre { get; set; } = string.Empty;
        public string? ResponsableTelefono { get; set; }
        public string ResponsableCorreo { get; set; } = string.Empty;

        // Solo lectura: identificador publico inmutable del equipo (no se postea ni se edita).
        public string CodigoPublico { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el nombre del grupo.")]
        [StringLength(LimitesTexto.NombreEquipo, ErrorMessage = LimitesTexto.Mensaje)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el nombre del altar.")]
        [StringLength(LimitesTexto.NombreAltar, ErrorMessage = LimitesTexto.Mensaje)]
        public string NombreAltar { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el nombre del difunto.")]
        [StringLength(LimitesTexto.NombreDifunto, ErrorMessage = LimitesTexto.Mensaje)]
        public string DifuntoNombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa la fecha de defunción.")]
        [DataType(DataType.Date)]
        public DateOnly DifuntoFechaDefuncion { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(-1));

        [Required(ErrorMessage = "Selecciona la carrera del altar.")]
        public int CarreraId { get; set; }

        [Required(ErrorMessage = "Ingresa la matrícula del maestro encargado.")]
        public string MaestroEncargadoIdentificador { get; set; } = string.Empty;

        [StringLength(LimitesTexto.Ubicacion, ErrorMessage = LimitesTexto.Mensaje)]
        public string? UbicacionAltar { get; set; }


        public DateTime CreadoEn { get; set; }

        public List<CatalogoCarrera> Carreras { get; set; } = new();
        public List<RegistranteEquipo> Integrantes { get; set; } = new();
        // A1: invitaciones enviadas que la persona aun no acepta. Solo se muestra la matricula que el organizador
        // escribio, no el nombre (el organizador no debe enterarse de datos que la persona no compartio).
        public List<(int Id, string Identificador)> InvitacionesPendientes { get; set; } = new();

        // Info de maestro encargado para mostrarlo como "integrante" en la vista.
        public string? MaestroEncargadoNombre { get; set; }
        public bool MaestroEncargadoPendiente { get; set; }

        public bool PuedeEditar { get; set; }
        public bool ExigirMinimoUnAnioFallecimiento { get; set; }
        public bool FechaLimiteRequisitosPasada { get; set; }

        // La fecha en si, para poder decirle al organizador CUANDO vencio en vez de solo "ya paso".
        public DateTime? FechaLimiteRequisitos { get; set; }

        // Lo que falta por capturar (EquipoValidacionHelper): permite listar los pendientes concretos
        // en vez de un aviso generico de "ficha incompleta".
        public IReadOnlyList<string> FichaFaltantes { get; set; } = new List<string>();
        // Existe una evaluacion (preliminar o final): la ficha queda cerrada aunque no haya pasado la fecha.
        public bool TieneEvaluacion { get; set; }
    }
}
