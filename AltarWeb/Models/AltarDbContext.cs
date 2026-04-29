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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Soft-delete: filtro global para que las consultas normales ignoren jueces eliminados
            modelBuilder.Entity<Juez>().HasQueryFilter(j => !j.IsDeleted);

            // Soft-delete: filtro global para integrantes eliminados
            modelBuilder.Entity<Integrante>().HasQueryFilter(i => !i.IsDeleted);

            // FK Evaluacion → Juez: SET NULL al eliminar (en vez de CASCADE)
            modelBuilder.Entity<Evaluacion>()
                .HasOne(e => e.Juez)
                .WithMany()
                .HasForeignKey(e => e.JuezId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}