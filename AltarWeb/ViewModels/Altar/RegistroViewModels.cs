using System.ComponentModel.DataAnnotations;
using AltarWeb.Models.Registro;

namespace AltarWeb.ViewModels.Altar
{
    public class RegistroLoginViewModel
    {
        [Required(ErrorMessage = "Ingresa tu correo institucional.")]
        [EmailAddress]
        public string CorreoInstitucional { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu contraseña.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public class RegistroSignupViewModel
    {
        [Required(ErrorMessage = "Ingresa tu nombre completo.")]
        public string NombreCompleto { get; set; } = string.Empty;

        // El Tipo ya no se elige a mano: se infiere de la longitud (7 dígitos = Alumno,
        // 5 dígitos = Maestro/Administrativo) para evitar inconsistencias entre el selector
        // y el identificador capturado.
        [Required(ErrorMessage = "Ingresa tu identificador.")]
        [RegularExpression(@"^\d{5}$|^\d{7}$", ErrorMessage = "El identificador debe tener 5 dígitos (maestro/administrativo) o 7 dígitos (alumno).")]
        public string Identificador { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu correo institucional.")]
        [EmailAddress]
        public string CorreoInstitucional { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        public int? CatalogoGeneroId { get; set; }
        public int? CatalogoCarreraId { get; set; }
        public string? AutodescripcionCultural { get; set; }

        [Required(ErrorMessage = "Elige una contraseña.")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmarPassword { get; set; } = string.Empty;

        public List<CatalogoGenero> Generos { get; set; } = new();
        public List<CatalogoCarrera> Carreras { get; set; } = new();
    }

    public class RegistranteDashboardViewModel
    {
        public Registrante Registrante { get; set; } = null!;
        public Equipo? Equipo { get; set; }
        public bool EsOrganizador { get; set; }
        public bool FichaCompleta { get; set; }
        public bool EquipoEvaluado { get; set; }
        public List<Elemento> Elementos { get; set; } = new();
    }
}
