using System.Security.Cryptography;

namespace Concurso.Core.Seguridad
{
    // SEC-03: la contraseña del admin semilla nunca es un literal fijo. Se toma de SEED_ADMIN_PASSWORD
    // (sin default inseguro) o se genera aleatoria; el usuario de SEED_ADMIN_USUARIO o el default que
    // indique la app. Cada app crea su propia cuenta con estas credenciales y con cambio obligatorio.
    public static class CredencialesSemilla
    {
        public static (string Usuario, string Password, bool PasswordGenerada) Obtener(string usuarioPorDefecto)
        {
            var usuario = Environment.GetEnvironmentVariable("SEED_ADMIN_USUARIO");
            usuario = string.IsNullOrWhiteSpace(usuario) ? usuarioPorDefecto : usuario.Trim();

            var passwordEnv = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");
            var passwordGenerada = string.IsNullOrWhiteSpace(passwordEnv);
            var password = passwordGenerada ? GenerarPasswordAleatoria() : passwordEnv!;

            return (usuario, password, passwordGenerada);
        }

        private static string GenerarPasswordAleatoria()
        {
            const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
            return RandomNumberGenerator.GetString(alfabeto, 20);
        }
    }
}
