using System.ComponentModel.DataAnnotations;
using Catrinas.Web.Modelos;

namespace Catrinas.Web.ViewModels
{
    // ViewModels dedicados: ningun POST bindea entidades EF (SEC-12), asi que un cliente no puede fijar
    // campos como Id, Periodo, Rol, Estado, Lugar o los snapshots.

    public class NavContext
    {
        public string Activo { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public bool EsAdmin { get; set; }
        public string Rol => EsAdmin ? "Administración" : "Jurado";
        public string Iniciales => string.Concat(NombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }

    public class LoginParticipanteViewModel
    {
        [Required(ErrorMessage = "Ingresa tu correo institucional.")]
        [EmailAddress]
        public string CorreoInstitucional { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu contraseña.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public class DatosPersonalesViewModel
    {
        [Required(ErrorMessage = "Ingresa tu nombre completo.")]
        [StringLength(200)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu matrícula o número de empleado.")]
        [RegularExpression(@"^\d{5}$|^\d{7}$", ErrorMessage = "La matrícula o número de empleado debe tener 7 dígitos (alumno) o 5 dígitos (maestro/administrativo).")]
        public string Identificador { get; set; } = string.Empty;

        // Solo para identificadores de 5 digitos: "Maestro" (por defecto) o "Administrativo".
        public string? TipoPersonal { get; set; }

        [StringLength(20)]
        public string? Telefono { get; set; }
        public int? CatalogoGeneroId { get; set; }
        public int? CatalogoCarreraId { get; set; }
        [StringLength(500)]
        public string? AutodescripcionCultural { get; set; }

        public List<CatalogoGenero> Generos { get; set; } = new();
        public List<CatalogoCarrera> Carreras { get; set; } = new();
    }

    public class SignupViewModel : DatosPersonalesViewModel
    {
        [Required(ErrorMessage = "Ingresa tu correo institucional.")]
        [EmailAddress]
        [StringLength(254)]
        public string CorreoInstitucional { get; set; } = string.Empty;

        [Required(ErrorMessage = "Elige una contraseña.")]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmarPassword { get; set; } = string.Empty;
    }

    // Completar registro tras Google: el correo y el nombre se leen SIEMPRE de la identidad verificada
    // server-side (SEC-01); los campos de la vista son solo informativos.
    public class CompletarGoogleViewModel : DatosPersonalesViewModel
    {
        public string CorreoInstitucional { get; set; } = string.Empty;
    }

    public class SolicitudJuradoViewModel
    {
        [Required(ErrorMessage = "Ingresa tu nombre completo.")]
        [StringLength(200)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu correo institucional.")]
        [EmailAddress]
        [StringLength(254)]
        public string CorreoInstitucional { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu número de empleado o matrícula.")]
        [RegularExpression(@"^\d{5}$|^\d{7}$", ErrorMessage = "La matrícula o número de empleado debe tener 5 o 7 dígitos.")]
        public string Identificador { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        public string? ConfirmarPassword { get; set; }

        public bool DesdeGoogle { get; set; }
    }

    public class InscripcionFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Ingresa el nombre de la inscripción.")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona la carrera.")]
        [Range(1, int.MaxValue, ErrorMessage = "Selecciona la carrera.")]
        public int CarreraId { get; set; }

        [RegularExpression(@"^\d{5}$", ErrorMessage = "El maestro encargado se identifica con 5 dígitos.")]
        public string? MaestroEncargadoIdentificador { get; set; }

        [StringLength(32)]
        public string? AltarExternalId { get; set; }

        [StringLength(150)]
        public string? AltarNombreSnapshot { get; set; }

        public List<CatalogoCarrera> Carreras { get; set; } = new();
    }

    public class PanelParticipanteViewModel
    {
        public Registrante Registrante { get; set; } = null!;
        public Inscripcion? Inscripcion { get; set; }
        public bool EsResponsable { get; set; }
        public IReadOnlyList<string> Faltantes { get; set; } = Array.Empty<string>();
        public string? MotivoBloqueo { get; set; }
        public bool PuedeCrear { get; set; }
        public string? MotivoNoPuedeCrear { get; set; }
        public string Periodo { get; set; } = string.Empty;
        // A1: invitaciones que la persona puede aceptar o rechazar (solo mientras no esta en una inscripcion).
        public List<InvitacionRecibidaViewModel> Invitaciones { get; set; } = new();
    }

    public class InvitacionRecibidaViewModel
    {
        public int Id { get; set; }
        public string Inscripcion { get; set; } = string.Empty;
        public string Carrera { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
    }

    public class FilaInscripcionViewModel
    {
        public int InscripcionId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Carrera { get; set; } = string.Empty;
        public string? Personificador { get; set; }
        public int Integrantes { get; set; }
        public EstadoEvaluacion? Estado { get; set; }
        public int? EvaluacionId { get; set; }
        public decimal? Nota { get; set; }
        public int? Lugar { get; set; }
        public IReadOnlyList<string> Faltantes { get; set; } = Array.Empty<string>();
        public string? AltarExternalId { get; set; }
    }

    public class EvaluarViewModel
    {
        public int InscripcionId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Carrera { get; set; } = string.Empty;
        public string? Personificador { get; set; }
        public List<(string Nombre, RolInscripcion Rol)> Integrantes { get; set; } = new();
        public string? MaestroEncargado { get; set; }
        public string? AltarExternalId { get; set; }
        public string? AltarNombre { get; set; }
        public IReadOnlyList<string> Faltantes { get; set; } = Array.Empty<string>();

        public EstadoEvaluacion? Estado { get; set; }
        public int? EvaluacionId { get; set; }
        public string? RowVersion { get; set; }
        public decimal Vestuario { get; set; }
        public decimal Maquillaje { get; set; }
        public decimal Accesorios { get; set; }
        public decimal OriginalidadCreatividad { get; set; }
        public decimal ApegoTradiciones { get; set; }
        public ConfiguracionPeriodo Pesos { get; set; } = new();
        public bool PesosValidos { get; set; }
    }

    public class ConfiguracionViewModel
    {
        public string Periodo { get; set; } = string.Empty;
        [DataType(DataType.Date)] public DateTime? FechaLimiteInscripcion { get; set; }
        [DataType(DataType.Date)] public DateTime? FechaLimiteRequisitos { get; set; }

        [Range(0, 100)] public decimal PesoVestuarioPct { get; set; }
        [Range(0, 100)] public decimal PesoMaquillajePct { get; set; }
        [Range(0, 100)] public decimal PesoAccesoriosPct { get; set; }
        [Range(0, 100)] public decimal PesoOriginalidadCreatividadPct { get; set; }
        [Range(0, 100)] public decimal PesoApegoTradicionesPct { get; set; }
        public decimal SumaPesos => PesoVestuarioPct + PesoMaquillajePct + PesoAccesoriosPct + PesoOriginalidadCreatividadPct + PesoApegoTradicionesPct;

        // PRIV-01: piso no anulable (el helper de Core aplica el mismo piso aunque la BD trajera menos).
        [Range(PrivacidadReporteHelper.UmbralMinimo, 100, ErrorMessage = "El umbral debe estar entre 3 y 100 para proteger datos demográficos sensibles.")]
        public int UmbralAgrupacionDemografica { get; set; } = 5;
    }

    public class CrearJuezViewModel
    {
        [Required(ErrorMessage = "Ingresa el nombre.")]
        [StringLength(200)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el usuario.")]
        [StringLength(100)]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa una contraseña temporal.")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        public string Password { get; set; } = string.Empty;

        public bool EsAdmin { get; set; }
    }
}
