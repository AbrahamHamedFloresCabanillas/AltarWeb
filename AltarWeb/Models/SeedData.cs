using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Models
{
    public static class SeedData
    {
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

                // SI NO HAY JUECES, AGREGAMOS AL ADMINISTRADOR PRINCIPAL:
                context.Jueces.AddRange(
                    new Juez
                    {
                        Usuario = "abram",
                        Password = "1234",
                        Rol = "Admin",
                        NombreCompleto = "Abram (Administrador)"
                    }
                );

                context.SaveChanges();
            }
        }
    }
}