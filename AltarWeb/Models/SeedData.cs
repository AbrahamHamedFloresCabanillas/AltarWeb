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
                // Revisar si la base de datos existe, si no, la crea
                context.Database.EnsureCreated();

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