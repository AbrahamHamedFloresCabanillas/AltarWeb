using System.ComponentModel.DataAnnotations;
using Concurso.Core.Identidad;

namespace Catrinas.Web.Modelos
{
    // Modelo de dominio del Concurso de Catrinas (docs/01-decision-arquitectura.md §7.1). Es propio de
    // esta aplicacion: no reutiliza ninguna entidad de Altares.

    public enum TipoRegistrante
    {
        Alumno,
        Maestro,
        Administrativo
    }

    // ADR §7.1 (3, 4): exactamente un integrante personifica a la Catrina; el resto son integrantes.
    public enum RolInscripcion
    {
        Integrante,
        Personificador
    }

    public enum EstadoEvaluacion
    {
        Preliminar,
        Final
    }

    public class CatalogoCarrera
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public bool Activo { get; set; } = true;
    }

    public class CatalogoGenero
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public bool Activo { get; set; } = true;
    }

    // Participante (alumno, maestro o administrativo). ADR §7.1 (10): mismos datos personales y
    // demograficos que en Altares, en la base de Catrinas.
    public class Registrante : IValidatableObject
    {
        public int Id { get; set; }
        public TipoRegistrante Tipo { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Identificador { get; set; } = string.Empty;
        public string CorreoInstitucional { get; set; } = string.Empty;
        public string? Telefono { get; set; }

        public string? PasswordHash { get; set; }
        public string ProveedorAuth { get; set; } = "Local";

        public int? CatalogoGeneroId { get; set; }
        public CatalogoGenero? CatalogoGenero { get; set; }
        public int? CatalogoCarreraId { get; set; }
        public CatalogoCarrera? CatalogoCarrera { get; set; }

        // Dato sensible: solo se captura y se usa agregado con el umbral de privacidad (vision.md §12.2).
        public string? AutodescripcionCultural { get; set; }

        public bool Activo { get; set; } = true;
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        public ICollection<MiembroInscripcion> Membresias { get; set; } = new List<MiembroInscripcion>();

        // Alumno = matricula de 7 digitos; Maestro/Administrativo = 5 digitos; correo @uabc.edu.mx.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var longitud = Tipo == TipoRegistrante.Alumno ? 7 : 5;
            if (string.IsNullOrEmpty(Identificador) || Identificador.Length != longitud || !Identificador.All(char.IsAsciiDigit))
            {
                yield return new ValidationResult(
                    Tipo == TipoRegistrante.Alumno
                        ? "La matrícula de alumno debe tener exactamente 7 dígitos."
                        : "La matrícula o número de empleado debe tener exactamente 5 dígitos.",
                    new[] { nameof(Identificador) });
            }

            if (!DominioInstitucional.EsCorreoInstitucional(CorreoInstitucional))
            {
                yield return new ValidationResult("El correo debe pertenecer al dominio @uabc.edu.mx.", new[] { nameof(CorreoInstitucional) });
            }
        }
    }

    // Cuenta del jurado o de administracion de Catrinas (independiente de la de Altares).
    public class Juez
    {
        public int Id { get; set; }
        public string? Usuario { get; set; }
        public string Password { get; set; } = string.Empty;
        public string Rol { get; set; } = RolesJuez.Juez;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? CorreoInstitucional { get; set; }
        public string? Identificador { get; set; }
        public string ProveedorAuth { get; set; } = "Local";
        // Auto-registro: queda pendiente hasta que un administrador lo apruebe.
        public bool Pendiente { get; set; }
        public bool DebeCambiarPassword { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime? FechaBaja { get; set; }
    }

    public static class RolesJuez
    {
        public const string Juez = "Juez";
        public const string Admin = "Admin";
    }

    // ADR §7.1 (1, 2, 5): una inscripcion por periodo, de un equipo de 1 a N personas. Quien la crea es el
    // responsable. El maestro encargado es opcional (§7.1 (9)). La referencia a Altares es blanda (§2.6):
    // solo un identificador publico y un nombre capturados, sin FK ni consulta a la otra base.
    public class Inscripcion
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int CarreraId { get; set; }
        public CatalogoCarrera Carrera { get; set; } = null!;
        public string Periodo { get; set; } = string.Empty;

        public int ResponsableId { get; set; }
        public Registrante Responsable { get; set; } = null!;

        public int? MaestroEncargadoId { get; set; }
        public Registrante? MaestroEncargado { get; set; }

        public string? AltarExternalId { get; set; }
        public string? AltarNombreSnapshot { get; set; }

        public bool Activo { get; set; } = true;
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
        public DateTime? ActualizadoEn { get; set; }

        public ICollection<MiembroInscripcion> Miembros { get; set; } = new List<MiembroInscripcion>();
        public EvaluacionCatrina? Evaluacion { get; set; }
    }

    public class MiembroInscripcion
    {
        public int InscripcionId { get; set; }
        public Inscripcion Inscripcion { get; set; } = null!;
        public int RegistranteId { get; set; }
        public Registrante Registrante { get; set; } = null!;
        public RolInscripcion Rol { get; set; } = RolInscripcion.Integrante;
        // Copia del periodo de la inscripcion: permite que la BD imponga "una inscripcion por persona y
        // periodo" con un indice unico (RegistranteId, Periodo).
        public string Periodo { get; set; } = string.Empty;
        public DateTime FechaIngreso { get; set; } = DateTime.UtcNow;
    }

    // A1 (pruebas adversariales 2026-10-21): el responsable ya no agrega integrantes directamente; los invita y la
    // persona acepta o rechaza desde su panel. Mientras no acepte no es miembro ni queda atada a la inscripcion.
    public class InvitacionInscripcion
    {
        public int Id { get; set; }
        public int InscripcionId { get; set; }
        public Inscripcion Inscripcion { get; set; } = null!;
        public int RegistranteId { get; set; }
        public Registrante Registrante { get; set; } = null!;
        // Copia del periodo (como MiembroInscripcion): al aceptar se descartan sus demas invitaciones del periodo.
        public string Periodo { get; set; } = string.Empty;
        public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
    }

    // Evaluacion del jurado de Catrinas: cinco criterios 0-10 y nota ponderada. Guarda los pesos con los
    // que se calculo y un snapshot de la inscripcion, para que el historico no dependa de cambios
    // posteriores en la configuracion ni en los registros.
    public class EvaluacionCatrina
    {
        public int Id { get; set; }
        public int InscripcionId { get; set; }
        public Inscripcion Inscripcion { get; set; } = null!;
        public int? JuezId { get; set; }
        public Juez? Juez { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public EstadoEvaluacion Estado { get; set; } = EstadoEvaluacion.Preliminar;

        public decimal Vestuario { get; set; }
        public decimal Maquillaje { get; set; }
        public decimal Accesorios { get; set; }
        public decimal OriginalidadCreatividad { get; set; }
        public decimal ApegoTradiciones { get; set; }

        public decimal PesoVestuario { get; set; }
        public decimal PesoMaquillaje { get; set; }
        public decimal PesoAccesorios { get; set; }
        public decimal PesoOriginalidadCreatividad { get; set; }
        public decimal PesoApegoTradiciones { get; set; }

        public decimal NotaCatrina { get; set; }
        // 1, 2 o 3 dentro de su carrera y periodo (con empates); null fuera del podio o si no es Final.
        public int? Lugar { get; set; }

        public string SnapshotNombreInscripcion { get; set; } = string.Empty;
        public string SnapshotCarrera { get; set; } = string.Empty;
        public string SnapshotPersonificador { get; set; } = string.Empty;
        public string? SnapshotMaestroEncargado { get; set; }
        public string? SnapshotAltarExternalId { get; set; }
        public string? SnapshotAltarNombre { get; set; }

        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
        public DateTime? ActualizadoEn { get; set; }
        public DateTime? FinalizadaEn { get; set; }

        // Concurrencia optimista: dos jueces guardando la misma evaluacion a la vez no se pisan.
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<IntegranteEvaluado> Integrantes { get; set; } = new List<IntegranteEvaluado>();
    }

    // Snapshot de los integrantes al evaluar: alimenta las constancias aunque luego cambien los registros.
    public class IntegranteEvaluado
    {
        public int Id { get; set; }
        public int EvaluacionId { get; set; }
        public EvaluacionCatrina Evaluacion { get; set; } = null!;
        public int? RegistranteId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Identificador { get; set; } = string.Empty;
        public string? CorreoInstitucional { get; set; }
        public RolInscripcion Rol { get; set; }
    }

    // Configuracion por periodo, propia de Catrinas (periodos independientes de Altares).
    public class ConfiguracionPeriodo
    {
        public int Id { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public DateTime? FechaLimiteInscripcion { get; set; }
        public DateTime? FechaLimiteRequisitos { get; set; }

        public decimal PesoVestuario { get; set; } = 0.25m;
        public decimal PesoMaquillaje { get; set; } = 0.20m;
        public decimal PesoAccesorios { get; set; } = 0.10m;
        public decimal PesoOriginalidadCreatividad { get; set; } = 0.20m;
        public decimal PesoApegoTradiciones { get; set; } = 0.25m;

        // Umbral de agrupacion demografica para la reporteria futura (piso en PrivacidadReporteHelper).
        public int UmbralAgrupacionDemografica { get; set; } = 5;

        public decimal SumaPesos => PesoVestuario + PesoMaquillaje + PesoAccesorios + PesoOriginalidadCreatividad + PesoApegoTradiciones;
    }
}
