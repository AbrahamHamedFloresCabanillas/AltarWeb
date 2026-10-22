using System.ComponentModel.DataAnnotations;
using AltarWeb.Models.Registro;
using Microsoft.AspNetCore.Http;

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
        public string CodigoPublico { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string NombreAltar { get; set; } = string.Empty;
        public string Carrera { get; set; } = string.Empty;
        public int IntegrantesCount { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public string? MaestroEncargado { get; set; }
        public string? UbicacionAltar { get; set; }
        public bool Evaluado { get; set; }

        // Estado de la Ficha de Registro. Son datos distintos a proposito:
        // FichaCompleta   -> los datos requeridos estan capturados (EquipoValidacionHelper).
        // FichaValidada   -> un administrador la reviso y la acepto para seguimiento.
        // FichaFaltantes  -> que falta exactamente, para poder decirlo en vez de solo "pendiente".
        public bool FichaCompleta { get; set; }
        public bool FichaValidada { get; set; }
        public DateTime? FichaValidadaEn { get; set; }
        public IReadOnlyList<string> FichaFaltantes { get; set; } = new List<string>();
    }

    // SEC-12: CrearJuez bindeaba la entidad Juez completa (over-posting: Id, IsDeleted, FechaEliminado,
    // Pendiente, CorreoInstitucional, ProveedorAuth quedaban expuestos al binder). Solo estos 4 campos
    // son capturables desde el formulario; el resto se fija explicitamente en el controlador.
    public class CrearJuezViewModel
    {
        [Required(ErrorMessage = "Ingresa el nombre completo.")]
        [StringLength(LimitesTexto.NombrePersona, ErrorMessage = LimitesTexto.Mensaje)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa un usuario.")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa una contraseña.")]
        public string Password { get; set; } = string.Empty;

        public string Rol { get; set; } = "Juez";
    }

    // Edicion de un Juez/Admin desde el backoffice. Mismo criterio que CrearJuezViewModel: solo los
    // campos capturables; Id/IsDeleted/Pendiente/ProveedorAuth se leen y fijan en el controlador.
    public class EditarJuezViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Ingresa el nombre completo.")]
        [StringLength(LimitesTexto.NombrePersona, ErrorMessage = LimitesTexto.Mensaje)]
        public string NombreCompleto { get; set; } = string.Empty;

        // Solo aplica a cuentas locales; las de Google inician sesion con CorreoInstitucional.
        public string? Usuario { get; set; }

        public string Rol { get; set; } = "Juez";

        // Vacia = no se cambia. Si se captura, la cuenta queda con DebeCambiarPassword=true para
        // que la contrasena fijada por el admin sea de un solo uso.
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string? NuevaPassword { get; set; }

        // Solo lectura en la vista.
        public string ProveedorAuth { get; set; } = "Local";
        public string? CorreoInstitucional { get; set; }
        public bool EsCuentaLocal => ProveedorAuth == "Local";
        public bool EsLaPropiaCuenta { get; set; }
        public bool Pendiente { get; set; }
        public bool Inactivo { get; set; }
    }

    public class EditarRegistranteViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Ingresa el nombre completo.")]
        [StringLength(LimitesTexto.NombrePersona, ErrorMessage = LimitesTexto.Mensaje)]
        public string NombreCompleto { get; set; } = string.Empty;

        // A diferencia del signup, aqui el admin SI elige el tipo (puede corregir un Administrativo
        // que se registro como Maestro); la longitud del identificador se valida contra ese tipo
        // con la misma regla del modelo (Registrante.Validate).
        public TipoRegistrante Tipo { get; set; }

        [Required(ErrorMessage = "Ingresa el identificador.")]
        public string Identificador { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa el correo institucional.")]
        [StringLength(LimitesTexto.Correo, ErrorMessage = LimitesTexto.Mensaje)]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string CorreoInstitucional { get; set; } = string.Empty;

        [StringLength(LimitesTexto.Telefono, ErrorMessage = LimitesTexto.Mensaje)]
        public string? Telefono { get; set; }
        public int? CatalogoGeneroId { get; set; }
        public int? CatalogoCarreraId { get; set; }
        [StringLength(LimitesTexto.Autodescripcion, ErrorMessage = LimitesTexto.Mensaje)]
        public string? AutodescripcionCultural { get; set; }

        // Solo lectura en la vista.
        public string ProveedorAuth { get; set; } = "Local";
        public bool Activo { get; set; }
        public DateTime CreadoEn { get; set; }
        public string? EquipoActual { get; set; }

        public List<CatalogoGenero> Generos { get; set; } = new();
        public List<CatalogoCarrera> Carreras { get; set; } = new();
    }

    // Ficha de Registro editada por el administrador (vision.md 6.2). Comparte los campos de
    // FichaViewModel, pero no sus candados: el admin ignora las fechas limite y puede corregir la
    // ficha aun con evaluacion registrada (la lista de integrantes no, ver PuedeEditarIntegrantes).
    public class EditarEquipoViewModel
    {
        public int Id { get; set; }

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

        // Solo lectura en la vista.
        public string Periodo { get; set; } = string.Empty;
        public DateTime CreadoEn { get; set; }
        public string? OrganizadorNombre { get; set; }
        public int? CreadoPorRegistranteId { get; set; }
        public string? MaestroEncargadoNombre { get; set; }
        public bool MaestroEncargadoPendiente { get; set; }
        public bool TieneEvaluacion { get; set; }
        public string? EstadoEvaluacion { get; set; }
        // Con evaluacion registrada la lista queda cerrada: EvaluacionIntegrante y las constancias
        // salen de ella. Los datos de la ficha si se pueden corregir (con aviso en la vista).
        public bool PuedeEditarIntegrantes => !TieneEvaluacion;

        public bool FichaValidada { get; set; }
        public DateTime? FichaValidadaEn { get; set; }
        public IReadOnlyList<string> FichaFaltantes { get; set; } = new List<string>();
        public bool TieneNarrador { get; set; }

        public List<CatalogoCarrera> Carreras { get; set; } = new();
        public List<RegistranteEquipo> Integrantes { get; set; } = new();
    }

    public class ConfiguracionPeriodoAdminViewModel
    {
        public string Periodo { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? FechaLimiteInscripcion { get; set; }

        [DataType(DataType.Date)]
        public DateTime? FechaLimiteRequisitos { get; set; }

        // Ruta web del PDF ya guardado (p. ej. /uploads/recorridos/recorrido-2026-2.pdf).
        public string? RecorridoPdf { get; set; }

        // Archivo nuevo subido desde el formulario; si viene null se conserva el actual.
        public IFormFile? ArchivoRecorrido { get; set; }

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
