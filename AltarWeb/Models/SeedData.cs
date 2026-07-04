using System.Security.Cryptography;
using AltarWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AltarWeb.Models
{
    public static class SeedData
    {
        // SEC-03: la contraseña semilla ya no es un literal fijo. Se toma de la variable de entorno
        // SEED_ADMIN_PASSWORD (sin default inseguro) o, si no esta definida, se genera aleatoria y se
        // registra una sola vez en el log de arranque. En ambos casos queda hasheada con PBKDF2 y con
        // DebeCambiarPassword=true, para forzar su cambio en el primer login.
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new AltarDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<AltarDbContext>>()))
            {
                // Aplica las migraciones pendientes (el esquema se gestiona via EF Core migrations,
                // no via EnsureCreated, para no perder el historial de __EFMigrationsHistory).
                context.Database.Migrate();

                // Si ya hay jueces registrados, no hacemos nada
                // Nota: IgnoreQueryFilters() para incluir soft-deleted en la verificación
                if (context.Jueces.IgnoreQueryFilters().Any())
                {
                    return;
                }

                var usuario = Environment.GetEnvironmentVariable("SEED_ADMIN_USUARIO");
                usuario = string.IsNullOrWhiteSpace(usuario) ? "abram" : usuario.Trim();

                var passwordEnv = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");
                var passwordGenerada = string.IsNullOrWhiteSpace(passwordEnv);
                var passwordPlano = passwordGenerada ? GenerarPasswordAleatoria() : passwordEnv!;

                context.Jueces.Add(new Juez
                {
                    Usuario = usuario,
                    Password = PasswordHashService.HashPassword(passwordPlano),
                    Rol = "Admin",
                    NombreCompleto = "Abram (Administrador)",
                    DebeCambiarPassword = true
                });

                context.SaveChanges();

                if (passwordGenerada)
                {
                    var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
                    logger.LogWarning(
                        "Cuenta administradora semilla creada. Usuario: '{Usuario}'. Password temporal (solo se muestra esta vez): '{Password}'. " +
                        "Debe cambiarse en el primer login. Para fijar una password propia, define SEED_ADMIN_PASSWORD antes del primer arranque.",
                        usuario, passwordPlano);
                }
            }
        }

        private static string GenerarPasswordAleatoria()
        {
            const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
            return RandomNumberGenerator.GetString(alfabeto, 20);
        }
    }
}
