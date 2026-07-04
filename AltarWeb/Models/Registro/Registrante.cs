using System.ComponentModel.DataAnnotations;

namespace AltarWeb.Models.Registro
{
    public class Registrante : IValidatableObject
    {
        public int Id { get; set; }

        [Required]
        public TipoRegistrante Tipo { get; set; }

        [Required]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required]
        public string Identificador { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string CorreoInstitucional { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        // Autenticacion local (patron identico a AltarWeb.Models.Alumno para consistencia con el
        // portal legado). vision.md 4.1 no lista estos campos, pero 5 (Portal de Registro) exige
        // login local con contrasena, asi que son necesarios para que el flujo funcione.
        public string? PasswordHash { get; set; }
        public string ProveedorAuth { get; set; } = "Local";

        public int? CatalogoGeneroId { get; set; }
        public virtual CatalogoGenero? CatalogoGenero { get; set; }

        public int? CatalogoCarreraId { get; set; }
        public virtual CatalogoCarrera? CatalogoCarrera { get; set; }

        public string? AutodescripcionCultural { get; set; }

        public bool Activo { get; set; } = true;
        public DateTime CreadoEn { get; set; } = DateTime.Now;

        public virtual ICollection<RegistranteEquipo> Equipos { get; set; } = new List<RegistranteEquipo>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var longitudEsperada = Tipo == TipoRegistrante.Alumno ? 7 : 5;
            var esValido = !string.IsNullOrEmpty(Identificador)
                && Identificador.Length == longitudEsperada
                && Identificador.All(char.IsDigit);

            if (!esValido)
            {
                yield return new ValidationResult(
                    Tipo == TipoRegistrante.Alumno
                        ? "La matricula de Alumno debe tener exactamente 7 digitos numericos."
                        : "La matricula o numero de empleado de Maestro/Administrativo debe tener exactamente 5 digitos numericos.",
                    new[] { nameof(Identificador) });
            }

            if (!string.IsNullOrWhiteSpace(CorreoInstitucional)
                && !CorreoInstitucional.Trim().EndsWith("@uabc.edu.mx", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ValidationResult(
                    "El correo institucional debe pertenecer al dominio @uabc.edu.mx.",
                    new[] { nameof(CorreoInstitucional) });
            }
        }
    }
}
