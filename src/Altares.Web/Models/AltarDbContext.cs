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

        // --- Modelo nuevo (vision.md v2.4) ---
        public DbSet<Registro.CatalogoGenero> CatalogoGeneros { get; set; }
        public DbSet<Registro.CatalogoCarrera> CatalogoCarreras { get; set; }
        public DbSet<Registro.Registrante> Registrantes { get; set; }
        public DbSet<Registro.Equipo> EquiposConcurso { get; set; }
        public DbSet<Registro.RegistranteEquipo> RegistranteEquipos { get; set; }
        public DbSet<Registro.Difunto> Difuntos { get; set; }
        public DbSet<Registro.Elemento> Elementos { get; set; }
        public DbSet<Registro.Evaluacion> EvaluacionesConcurso { get; set; }
        public DbSet<Registro.EvaluacionIntegrante> EvaluacionIntegrantes { get; set; }
        public DbSet<Registro.ElementoEvaluado> ElementosEvaluados { get; set; }
        public DbSet<Registro.EvaluacionCatrina> EvaluacionesCatrina { get; set; }
        public DbSet<Registro.ConfiguracionPeriodo> ConfiguracionesPeriodo { get; set; }

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

            // Modelo nuevo: cada entidad de AltarWeb.Models.Registro trae su propia
            // IEntityTypeConfiguration<T> en Models/Registro/Configurations. Se aplican una por
            // una (en vez de ApplyConfigurationsFromAssembly) para poder escalonar las migraciones.
            modelBuilder.ApplyConfiguration(new Registro.Configurations.CatalogoGeneroConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.CatalogoCarreraConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.RegistranteConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.EquipoConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.RegistranteEquipoConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.DifuntoConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.ElementoConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.EvaluacionConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.EvaluacionIntegranteConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.ElementoEvaluadoConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.EvaluacionCatrinaConfiguration());
            modelBuilder.ApplyConfiguration(new Registro.Configurations.ConfiguracionPeriodoConfiguration());
        }
    }
}
