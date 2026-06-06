using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Models
{
    public class AltarDbContext : DbContext
    {
        public AltarDbContext(DbContextOptions<AltarDbContext> options) : base(options) { }

        public DbSet<Juez> Jueces { get; set; }
        public DbSet<Evaluacion> Evaluaciones { get; set; }
        public DbSet<Integrante> Integrantes { get; set; }
        public DbSet<Alumno> Alumnos { get; set; }
        public DbSet<Equipo> Equipos { get; set; }
        public DbSet<AlumnoEquipo> AlumnoEquipos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Juez>().HasQueryFilter(j => !j.IsDeleted);
            modelBuilder.Entity<Integrante>().HasQueryFilter(i => !i.IsDeleted);
            modelBuilder.Entity<Alumno>().HasQueryFilter(a => a.Activo);
            modelBuilder.Entity<Equipo>().HasQueryFilter(e => e.Activo);
            modelBuilder.Entity<AlumnoEquipo>().HasQueryFilter(ae => ae.Alumno.Activo && ae.Equipo.Activo);

            modelBuilder.Entity<Alumno>()
                .HasIndex(a => a.Matricula)
                .IsUnique();

            modelBuilder.Entity<Alumno>()
                .HasIndex(a => a.CorreoElectronico)
                .IsUnique();

            modelBuilder.Entity<Equipo>()
                .HasIndex(e => new { e.NombreEquipo, e.PeriodoAcademico })
                .IsUnique();

            modelBuilder.Entity<AlumnoEquipo>()
                .HasKey(ae => new { ae.AlumnoId, ae.EquipoId });

            modelBuilder.Entity<AlumnoEquipo>()
                .HasOne(ae => ae.Alumno)
                .WithMany(a => a.Equipos)
                .HasForeignKey(ae => ae.AlumnoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AlumnoEquipo>()
                .HasOne(ae => ae.Equipo)
                .WithMany(e => e.Integrantes)
                .HasForeignKey(ae => ae.EquipoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Equipo>()
                .HasOne(e => e.CreadoPorAlumno)
                .WithMany()
                .HasForeignKey(e => e.CreadoPorAlumnoId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Evaluacion>()
                .HasOne(e => e.Juez)
                .WithMany()
                .HasForeignKey(e => e.JuezId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Evaluacion>()
                .HasOne(e => e.Equipo)
                .WithMany(e => e.Evaluaciones)
                .HasForeignKey(e => e.EquipoId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Evaluacion>().Property(e => e.NotaTradicion).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Evaluacion>().Property(e => e.NotaPersonalizacion).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Evaluacion>().Property(e => e.NotaEstetica).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Evaluacion>().Property(e => e.NotaTradicionFinal).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Evaluacion>().Property(e => e.NotaPersonalizacionFinal).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Evaluacion>().Property(e => e.NotaFinal).HasColumnType("decimal(18,2)");
        }
    }
}
