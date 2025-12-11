using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Models // <--- ASEGÚRATE QUE ESTO COINCIDA CON TU PROYECTO
{
    public class AltarDbContext : DbContext
    {
        public AltarDbContext(DbContextOptions<AltarDbContext> options) : base(options) { }

        // Aquí registramos las tablas de tu Base de Datos SQL
        public DbSet<Juez> Jueces { get; set; }
        public DbSet<Evaluacion> Evaluaciones { get; set; }
        public DbSet<Integrante> Integrantes { get; set; }
    }
}