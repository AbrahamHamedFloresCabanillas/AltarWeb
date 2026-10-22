using Catrinas.Web.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Web.Datos
{
    // Base de datos propia de Catrinas. Las reglas de integridad importantes viven tambien aqui (indices
    // unicos, FK y CHECK), no solo en los controladores.
    public class CatrinasDbContext : DbContext
    {
        public CatrinasDbContext(DbContextOptions<CatrinasDbContext> options) : base(options) { }

        public DbSet<CatalogoCarrera> Carreras => Set<CatalogoCarrera>();
        public DbSet<CatalogoGenero> Generos => Set<CatalogoGenero>();
        public DbSet<Registrante> Registrantes => Set<Registrante>();
        public DbSet<Juez> Jueces => Set<Juez>();
        public DbSet<Inscripcion> Inscripciones => Set<Inscripcion>();
        public DbSet<MiembroInscripcion> Miembros => Set<MiembroInscripcion>();
        public DbSet<InvitacionInscripcion> Invitaciones => Set<InvitacionInscripcion>();
        public DbSet<EvaluacionCatrina> Evaluaciones => Set<EvaluacionCatrina>();
        public DbSet<IntegranteEvaluado> IntegrantesEvaluados => Set<IntegranteEvaluado>();
        public DbSet<ConfiguracionPeriodo> Configuraciones => Set<ConfiguracionPeriodo>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<CatalogoCarrera>(e =>
            {
                e.ToTable("Carreras");
                e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
                e.HasIndex(x => x.Nombre).IsUnique();
                e.HasData(SemillaCatalogos.Carreras);
            });

            b.Entity<CatalogoGenero>(e =>
            {
                e.ToTable("Generos");
                e.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
                e.HasIndex(x => x.Nombre).IsUnique();
                e.HasData(SemillaCatalogos.Generos);
            });

            b.Entity<Registrante>(e =>
            {
                e.ToTable("Registrantes", t =>
                    t.HasCheckConstraint("CK_Registrantes_Tipo", "[Tipo] IN ('Alumno','Maestro','Administrativo')"));
                e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.NombreCompleto).HasMaxLength(200).IsRequired();
                e.Property(x => x.Identificador).HasMaxLength(7).IsRequired();
                e.Property(x => x.CorreoInstitucional).HasMaxLength(254).IsRequired();
                e.Property(x => x.AvisoPrivacidadVersion).HasMaxLength(20);
                e.Property(x => x.Telefono).HasMaxLength(20);
                e.Property(x => x.ProveedorAuth).HasMaxLength(20);
                e.Property(x => x.AutodescripcionCultural).HasMaxLength(500);
                e.HasIndex(x => x.Identificador).IsUnique();
                e.HasIndex(x => x.CorreoInstitucional).IsUnique();
                e.HasOne(x => x.CatalogoGenero).WithMany().HasForeignKey(x => x.CatalogoGeneroId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CatalogoCarrera).WithMany().HasForeignKey(x => x.CatalogoCarreraId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Juez>(e =>
            {
                e.ToTable("Jueces", t =>
                    t.HasCheckConstraint("CK_Jueces_Rol", "[Rol] IN ('Juez','Admin')"));
                e.Property(x => x.Usuario).HasMaxLength(100);
                e.Property(x => x.Rol).HasMaxLength(10).IsRequired();
                e.Property(x => x.NombreCompleto).HasMaxLength(200).IsRequired();
                e.Property(x => x.CorreoInstitucional).HasMaxLength(254);
                e.Property(x => x.Identificador).HasMaxLength(7);
                e.Property(x => x.AvisoPrivacidadVersion).HasMaxLength(20);
                e.Property(x => x.ProveedorAuth).HasMaxLength(20);
                e.HasIndex(x => x.Usuario).IsUnique().HasFilter("[Usuario] IS NOT NULL");
                e.HasIndex(x => x.CorreoInstitucional).IsUnique().HasFilter("[CorreoInstitucional] IS NOT NULL");
            });

            b.Entity<Inscripcion>(e =>
            {
                e.ToTable("Inscripciones", t =>
                    t.HasCheckConstraint("CK_Inscripciones_AltarExternalId",
                        "[AltarExternalId] IS NULL OR [AltarExternalId] LIKE 'ALT-[0-9][0-9][0-9][0-9]-[12]-[0-9][0-9][0-9][0-9]%'"));
                e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
                e.Property(x => x.Periodo).HasMaxLength(10).IsRequired();
                e.Property(x => x.AltarExternalId).HasMaxLength(ReferenciaAltar.LongitudMaximaId);
                e.Property(x => x.AltarNombreSnapshot).HasMaxLength(ReferenciaAltar.LongitudMaximaNombre);
                e.HasIndex(x => new { x.Nombre, x.Periodo }).IsUnique();
                e.HasOne(x => x.Carrera).WithMany().HasForeignKey(x => x.CarreraId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Responsable).WithMany().HasForeignKey(x => x.ResponsableId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.MaestroEncargado).WithMany().HasForeignKey(x => x.MaestroEncargadoId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<MiembroInscripcion>(e =>
            {
                e.ToTable("MiembrosInscripcion", t =>
                    t.HasCheckConstraint("CK_MiembrosInscripcion_Rol", "[Rol] IN ('Integrante','Personificador')"));
                e.HasKey(x => new { x.InscripcionId, x.RegistranteId });
                e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Periodo).HasMaxLength(10).IsRequired();
                // Una inscripcion de Catrina por persona y periodo (la fila se borra al salir de la inscripcion).
                e.HasIndex(x => new { x.RegistranteId, x.Periodo }).IsUnique();
                // Exactamente un Personificador por inscripcion como maximo (el minimo lo exige el Final).
                e.HasIndex(x => x.InscripcionId).IsUnique().HasFilter("[Rol] = 'Personificador'")
                    .HasDatabaseName("IX_MiembrosInscripcion_UnPersonificador");
                e.HasOne(x => x.Inscripcion).WithMany(i => i.Miembros).HasForeignKey(x => x.InscripcionId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Registrante).WithMany(r => r.Membresias).HasForeignKey(x => x.RegistranteId).OnDelete(DeleteBehavior.Restrict);
            });

            // A1: invitaciones pendientes de aceptar. Una por persona e inscripcion (reinvitar no duplica).
            b.Entity<InvitacionInscripcion>(e =>
            {
                e.ToTable("InvitacionesInscripcion");
                e.Property(x => x.Periodo).HasMaxLength(10).IsRequired();
                e.HasIndex(x => new { x.InscripcionId, x.RegistranteId }).IsUnique();
                e.HasIndex(x => new { x.RegistranteId, x.Periodo });
                e.HasOne(x => x.Inscripcion).WithMany().HasForeignKey(x => x.InscripcionId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Registrante).WithMany().HasForeignKey(x => x.RegistranteId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<EvaluacionCatrina>(e =>
            {
                e.ToTable("Evaluaciones", t =>
                {
                    t.HasCheckConstraint("CK_Evaluaciones_Estado", "[Estado] IN ('Preliminar','Final')");
                    t.HasCheckConstraint("CK_Evaluaciones_Criterios",
                        "[Vestuario] BETWEEN 0 AND 10 AND [Maquillaje] BETWEEN 0 AND 10 AND [Accesorios] BETWEEN 0 AND 10 " +
                        "AND [OriginalidadCreatividad] BETWEEN 0 AND 10 AND [ApegoTradiciones] BETWEEN 0 AND 10");
                    t.HasCheckConstraint("CK_Evaluaciones_Pesos",
                        "[PesoVestuario] + [PesoMaquillaje] + [PesoAccesorios] + [PesoOriginalidadCreatividad] + [PesoApegoTradiciones] = 1");
                    t.HasCheckConstraint("CK_Evaluaciones_Lugar", "[Lugar] IS NULL OR ([Lugar] BETWEEN 1 AND 3 AND [Estado] = 'Final')");
                    t.HasCheckConstraint("CK_Evaluaciones_Final", "[Estado] <> 'Final' OR [FinalizadaEn] IS NOT NULL");
                });
                e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Periodo).HasMaxLength(10).IsRequired();
                foreach (var criterio in new[] { nameof(EvaluacionCatrina.Vestuario), nameof(EvaluacionCatrina.Maquillaje), nameof(EvaluacionCatrina.Accesorios),
                             nameof(EvaluacionCatrina.OriginalidadCreatividad), nameof(EvaluacionCatrina.ApegoTradiciones), nameof(EvaluacionCatrina.NotaCatrina) })
                {
                    e.Property(criterio).HasColumnType("decimal(5,2)");
                }
                foreach (var peso in new[] { nameof(EvaluacionCatrina.PesoVestuario), nameof(EvaluacionCatrina.PesoMaquillaje), nameof(EvaluacionCatrina.PesoAccesorios),
                             nameof(EvaluacionCatrina.PesoOriginalidadCreatividad), nameof(EvaluacionCatrina.PesoApegoTradiciones) })
                {
                    e.Property(peso).HasColumnType("decimal(5,4)");
                }
                e.Property(x => x.SnapshotNombreInscripcion).HasMaxLength(150).IsRequired();
                e.Property(x => x.SnapshotCarrera).HasMaxLength(150).IsRequired();
                e.Property(x => x.SnapshotPersonificador).HasMaxLength(200).IsRequired();
                e.Property(x => x.SnapshotMaestroEncargado).HasMaxLength(200);
                e.Property(x => x.SnapshotAltarExternalId).HasMaxLength(ReferenciaAltar.LongitudMaximaId);
                e.Property(x => x.SnapshotAltarNombre).HasMaxLength(ReferenciaAltar.LongitudMaximaNombre);
                e.Property(x => x.RowVersion).IsRowVersion();
                // Una evaluacion por inscripcion.
                e.HasIndex(x => x.InscripcionId).IsUnique();
                e.HasIndex(x => new { x.Periodo, x.Estado });
                e.HasOne(x => x.Inscripcion).WithOne(i => i.Evaluacion).HasForeignKey<EvaluacionCatrina>(x => x.InscripcionId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Juez).WithMany().HasForeignKey(x => x.JuezId).OnDelete(DeleteBehavior.SetNull);
            });

            b.Entity<IntegranteEvaluado>(e =>
            {
                e.ToTable("IntegrantesEvaluados", t =>
                    t.HasCheckConstraint("CK_IntegrantesEvaluados_Rol", "[Rol] IN ('Integrante','Personificador')"));
                e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.NombreCompleto).HasMaxLength(200).IsRequired();
                e.Property(x => x.Identificador).HasMaxLength(7).IsRequired();
                e.Property(x => x.CorreoInstitucional).HasMaxLength(254);
                e.HasOne(x => x.Evaluacion).WithMany(ev => ev.Integrantes).HasForeignKey(x => x.EvaluacionId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.EvaluacionId, x.RegistranteId }).IsUnique().HasFilter("[RegistranteId] IS NOT NULL");
            });

            b.Entity<ConfiguracionPeriodo>(e =>
            {
                e.ToTable("ConfiguracionesPeriodo", t =>
                {
                    t.HasCheckConstraint("CK_ConfiguracionesPeriodo_Pesos",
                        "[PesoVestuario] + [PesoMaquillaje] + [PesoAccesorios] + [PesoOriginalidadCreatividad] + [PesoApegoTradiciones] = 1 " +
                        "AND [PesoVestuario] >= 0 AND [PesoMaquillaje] >= 0 AND [PesoAccesorios] >= 0 AND [PesoOriginalidadCreatividad] >= 0 AND [PesoApegoTradiciones] >= 0");
                    t.HasCheckConstraint("CK_ConfiguracionesPeriodo_Umbral", $"[UmbralAgrupacionDemografica] >= {PrivacidadReporteHelper.UmbralMinimo}");
                });
                e.Property(x => x.Periodo).HasMaxLength(10).IsRequired();
                e.HasIndex(x => x.Periodo).IsUnique();
                foreach (var peso in new[] { nameof(ConfiguracionPeriodo.PesoVestuario), nameof(ConfiguracionPeriodo.PesoMaquillaje), nameof(ConfiguracionPeriodo.PesoAccesorios),
                             nameof(ConfiguracionPeriodo.PesoOriginalidadCreatividad), nameof(ConfiguracionPeriodo.PesoApegoTradiciones) })
                {
                    e.Property(peso).HasColumnType("decimal(5,4)");
                }
                e.Ignore(x => x.SumaPesos);
            });
        }
    }

    public static class SemillaCatalogos
    {
        // Catalogo oficial de carreras de la FIM (vision.md Apendice C).
        public static readonly CatalogoCarrera[] Carreras =
        {
            new() { Id = 1, Nombre = "Lic. en Sistemas Computacionales", Orden = 1 },
            // B2 (2026-10-22, decision del propietario): "Ing. en Bioingeniería" en lugar de "Bioingeniero".
            new() { Id = 2, Nombre = "Ing. en Bioingeniería", Orden = 2 },
            new() { Id = 3, Nombre = "Ing. Aeroespacial", Orden = 3 },
            new() { Id = 4, Nombre = "Ing. Civil", Orden = 4 },
            new() { Id = 5, Nombre = "Ing. en Computación", Orden = 5 },
            new() { Id = 6, Nombre = "Ing. en Electrónica", Orden = 6 },
            new() { Id = 7, Nombre = "Ing. Eléctrico", Orden = 7 },
            new() { Id = 8, Nombre = "Ing. en Energías Renovables", Orden = 8 },
            new() { Id = 9, Nombre = "Ing. Industrial", Orden = 9 },
            new() { Id = 10, Nombre = "Ing. Mecánico", Orden = 10 },
            new() { Id = 11, Nombre = "Ing. en Mecatrónica", Orden = 11 },
            new() { Id = 12, Nombre = "Ing. en Semiconductores y Microelectrónica", Orden = 12 },
            new() { Id = 13, Nombre = "Ing. de Datos e Inteligencia Artificial", Orden = 13 }
        };

        // vision.md Apendice B.
        public static readonly CatalogoGenero[] Generos =
        {
            new() { Id = 1, Nombre = "Masculino", Orden = 1 },
            new() { Id = 2, Nombre = "Femenino", Orden = 2 },
            new() { Id = 3, Nombre = "No binario", Orden = 3 },
            new() { Id = 4, Nombre = "Prefiero no especificar", Orden = 4 },
            new() { Id = 5, Nombre = "Otro", Orden = 5 }
        };
    }
}
