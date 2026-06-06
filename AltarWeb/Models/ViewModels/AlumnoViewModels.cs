using System.ComponentModel.DataAnnotations;

namespace AltarWeb.Models.ViewModels
{
    public class AlumnoLoginViewModel
    {
        [Required(ErrorMessage = "La matrícula es obligatoria")]
        [RegularExpression(@"^\d+$", ErrorMessage = "La matrícula solo debe contener números")]
        public string Matricula { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        public string Password { get; set; } = string.Empty;
    }

    public class AlumnoRegistroViewModel
    {
        [Required(ErrorMessage = "El nombre completo es obligatorio")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "La matrícula es obligatoria")]
        [RegularExpression(@"^\d+$", ErrorMessage = "La matrícula solo debe contener números")]
        public string Matricula { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo institucional es obligatorio")]
        [EmailAddress(ErrorMessage = "Correo electrónico inválido")]
        [RegularExpression(@"^[^@]+@uabc\.edu\.mx$", ErrorMessage = "Debe ser un correo @uabc.edu.mx")]
        public string CorreoElectronico { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [MinLength(8, ErrorMessage = "Mínimo 8 caracteres")]
        public string Password { get; set; } = string.Empty;

        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
        public string ConfirmarPassword { get; set; } = string.Empty;
    }

    public class CompletarRegistroGoogleViewModel
    {
        public string CorreoElectronico { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "La matrícula es obligatoria")]
        [RegularExpression(@"^\d+$", ErrorMessage = "La matrícula solo debe contener números")]
        public string Matricula { get; set; } = string.Empty;
    }

    public class AlumnoDashboardViewModel
    {
        public Alumno Alumno { get; set; } = null!;
        public Equipo? Equipo { get; set; }
        public Evaluacion? Evaluacion { get; set; }
        public bool EsCreador { get; set; }
    }

    public class CrearEquipoViewModel
    {
        [Required(ErrorMessage = "El nombre del equipo es obligatorio")]
        public string NombreEquipo { get; set; } = string.Empty;

        public List<int> IntegranteIds { get; set; } = new();
    }

    public enum EstadoEvaluacion
    {
        SinEquipo,
        PendienteEvaluacion,
        Evaluado
    }

    public class MiEvaluacionViewModel
    {
        public EstadoEvaluacion Estado { get; set; }
        public string NombreEquipo { get; set; } = string.Empty;
        public string NombreDifunto { get; set; } = string.Empty;
        public string TipoAltar { get; set; } = string.Empty;
        public string Niveles { get; set; } = string.Empty;
        public int ElementosPresentes { get; set; }
        public int BonusTematicos { get; set; }
        public decimal NotaTradicionFinal { get; set; }
        public decimal NotaPersonalizacionFinal { get; set; }
        public decimal NotaEstetica { get; set; }
        public decimal NotaFinal { get; set; }
        public int EvaluacionId { get; set; }
        public bool PuedeDescargarConstancia { get; set; }
    }

    public class EditarEquipoViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del equipo es obligatorio")]
        public string NombreEquipo { get; set; } = string.Empty;

        public string PeriodoAcademico { get; set; } = string.Empty;
        public List<int> IntegranteIds { get; set; } = new();
    }
}
