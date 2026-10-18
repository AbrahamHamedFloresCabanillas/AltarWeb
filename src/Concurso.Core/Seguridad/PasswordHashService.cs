using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Concurso.Core.Seguridad
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
        private static readonly Lazy<string> HashSenuelo = new(() => Hasher.HashPassword(new object(), Guid.NewGuid().ToString("N")));

        public static string HashPassword(string password) => Hasher.HashPassword(new object(), password);

        // Usado solo por el fixup de arranque para distinguir "plaintext residual" (ninguno de los formatos
        // conocidos) de un hash ya migrado, sin volver a rehashear lo que ya esta bien.
        //
        // Se valida la ESTRUCTURA del valor, no basta con que PasswordHasher no lance excepcion: un texto
        // plano que tambien es Base64 valido (p. ej. "1234") hacia que VerifyHashedPassword devolviera
        // Failed sin lanzar FormatException, y se tomaba por hash (defecto encontrado el 2026-10-17).
        public static bool EsFormatoReconocido(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return false;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(valor);
            }
            catch (FormatException)
            {
                return false;
            }

            // SHA-256 legado: 44 caracteres Base64 = exactamente 32 bytes.
            if (valor.Length == LongitudHashLegadoSha256) return bytes.Length == 32;

            return EsFormatoIdentityV2(bytes) || EsFormatoIdentityV3(bytes);
        }

        // Identity V2: 0x00 | salt (16 bytes) | subkey (32 bytes).
        private static bool EsFormatoIdentityV2(byte[] bytes) => bytes.Length == 49 && bytes[0] == 0x00;

        // Identity V3: 0x01 | prf (uint32) | iteraciones (uint32) | longitud del salt (uint32) | salt | subkey.
        // Mismos minimos que valida PasswordHasher al decodificar: salt y subkey de al menos 128 bits.
        private static bool EsFormatoIdentityV3(byte[] bytes)
        {
            const int encabezado = 13;
            if (bytes.Length < encabezado || bytes[0] != 0x01) return false;

            var longitudSalt = (bytes[9] << 24) | (bytes[10] << 16) | (bytes[11] << 8) | bytes[12];
            if (longitudSalt < 16) return false;

            var longitudSubkey = bytes.Length - encabezado - longitudSalt;
            return longitudSubkey >= 16;
        }

        public static (bool EsValido, bool RequiereRehash) Verificar(string password, string? hashAlmacenado)
        {
            if (string.IsNullOrEmpty(hashAlmacenado))
            {
                // F4: cuenta inexistente (o solo de Google) respondia en ~5 ms contra ~120 ms de una cuenta
                // real, lo que permitia saber que correos estan registrados midiendo el tiempo. Se hace el
                // mismo trabajo de PBKDF2 contra un hash señuelo y se rechaza igual.
                Hasher.VerifyHashedPassword(new object(), HashSenuelo.Value, password ?? string.Empty);
                return (false, false);
            }

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
