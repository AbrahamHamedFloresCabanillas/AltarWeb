using System.ComponentModel.DataAnnotations;
using AltarWeb.Models.Registro;

namespace AltarWeb.ViewModels.Altar
{
    public class RegistroSignupJuezViewModel
    {
        [Required(ErrorMessage = "Ingresa tu nombre completo.")]
        [StringLength(LimitesTexto.NombrePersona, ErrorMessage = LimitesTexto.Mensaje)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu correo institucional.")]
        [StringLength(LimitesTexto.Correo, ErrorMessage = LimitesTexto.Mensaje)]
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

        [Required(ErrorMessage = "Ingresa tu matrícula o número de empleado.")]
        [RegularExpression(@"^\d{5}$|^\d{7}$", ErrorMessage = "La matrícula o número de empleado debe tener 7 dígitos (alumno) o 5 dígitos (maestro/administrativo).")]
        public string Identificador { get; set; } = string.Empty;

        [StringLength(LimitesTexto.Telefono, ErrorMessage = LimitesTexto.Mensaje)]
        public string? Telefono { get; set; }
        public int? CatalogoGeneroId { get; set; }
        public int? CatalogoCarreraId { get; set; }
        [StringLength(LimitesTexto.Autodescripcion, ErrorMessage = LimitesTexto.Mensaje)]
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
