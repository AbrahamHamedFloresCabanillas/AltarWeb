using System.ComponentModel.DataAnnotations;
using AltarWeb.Models.Registro;

namespace AltarWeb.ViewModels.Altar
{
    public class RegistroSignupJuezViewModel
    {
        [Required(ErrorMessage = "Ingresa tu nombre completo.")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu correo institucional.")]
        [EmailAddress]
        public string CorreoInstitucional { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu matrícula o número de empleado.")]
        [RegularExpression(@"^\d{5}$", ErrorMessage = "La matrícula o número de empleado debe tener exactamente 5 dígitos.")]
        public string Identificador { get; set; } = string.Empty;

        [Required(ErrorMessage = "Elige una contraseña.")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmarPassword { get; set; } = string.Empty;
    }

    // Se llega aqui despues de un login con Google cuyo correo no coincide con ningun
    // Registrante existente: se piden los datos que Google no proporciona.
    public class CompletarGoogleViewModel
    {
        public string NombreCompleto { get; set; } = string.Empty;
        public string CorreoInstitucional { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu identificador.")]
        [RegularExpression(@"^\d{5}$|^\d{7}$", ErrorMessage = "El identificador debe tener 5 dígitos (maestro/administrativo) o 7 dígitos (alumno).")]
        public string Identificador { get; set; } = string.Empty;

        public string? Telefono { get; set; }
        public int? CatalogoGeneroId { get; set; }
        public int? CatalogoCarreraId { get; set; }
        public string? AutodescripcionCultural { get; set; }

        public List<CatalogoGenero> Generos { get; set; } = new();
        public List<CatalogoCarrera> Carreras { get; set; } = new();
    }

    // Igual que arriba pero para jueces: Google no da matricula, y el juez ademas
    // queda pendiente de aprobacion por el administrador (igual que el auto-registro local).
    public class CompletarGoogleJuezViewModel
    {
        public string NombreCompleto { get; set; } = string.Empty;
        public string CorreoInstitucional { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu matrícula o número de empleado.")]
        [RegularExpression(@"^\d{5}$", ErrorMessage = "La matrícula o número de empleado debe tener exactamente 5 dígitos.")]
        public string Identificador { get; set; } = string.Empty;
    }
}
