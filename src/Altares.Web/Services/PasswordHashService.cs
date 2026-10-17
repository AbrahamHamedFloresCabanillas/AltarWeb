using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace AltarWeb.Services
{
    // SEC-02: reemplaza el SHA-256 sin salt usado antes para Registrante y Juez por PBKDF2
    // (Microsoft.AspNetCore.Identity.PasswordHasher<T>, ya incluido en el framework compartido de
    // ASP.NET Core; no agrega dependencias). El formato legado se distingue por longitud fija (44
    // caracteres Base64 de un SHA-256); el formato nuevo (V3 de PasswordHasher) nunca coincide con esa
    // longitud, asi que la migracion es "rehash-on-login": se detecta el formato viejo en un login
    // exitoso y se reemplaza por el nuevo sin invalidar la cuenta.
    public static class PasswordHashService
    {
        private const int LongitudHashLegadoSha256 = 44;
        private static readonly PasswordHasher<object> Hasher = new();

        public static string HashPassword(string password) => Hasher.HashPassword(new object(), password);

        // Usado solo por el fixup de arranque para distinguir "plaintext residual" (ninguno de los dos
        // formatos conocidos) de un hash ya migrado, sin volver a rehashear lo que ya esta bien.
        public static bool EsFormatoReconocido(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return false;
            if (valor.Length == LongitudHashLegadoSha256) return true;

            try
            {
                Hasher.VerifyHashedPassword(new object(), valor, string.Empty);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static (bool EsValido, bool RequiereRehash) Verificar(string password, string? hashAlmacenado)
        {
            if (string.IsNullOrEmpty(hashAlmacenado)) return (false, false);

            if (hashAlmacenado.Length == LongitudHashLegadoSha256)
            {
                var coincide = HashLegadoSha256(password) == hashAlmacenado;
                return (coincide, coincide);
            }

            try
            {
                var resultado = Hasher.VerifyHashedPassword(new object(), hashAlmacenado, password);
                return resultado switch
                {
                    PasswordVerificationResult.Success => (true, false),
                    PasswordVerificationResult.SuccessRehashNeeded => (true, true),
                    _ => (false, false)
                };
            }
            catch (FormatException)
            {
                return (false, false);
            }
        }

        private static string HashLegadoSha256(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
