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
                if (context.Jueces.Any())
                {
                    return;
                }

                // SI NO HAY JUECES, AGREGAMOS AL PRINCIPAL:
                context.Jueces.AddRange(
                    new Juez
                    {
                        Usuario = "abram",
                        Password = "1234"
                    }
                );

                context.SaveChanges();
            }
        }
    }
}