using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class EquipoConfiguration : IEntityTypeConfiguration<Equipo>
    {
        public void Configure(EntityTypeBuilder<Equipo> builder)
        {
            builder.ToTable("EquiposConcurso");

builder.HasIndex(e => new { e.Nombre, e.Periodo }).IsUnique();

            // CodigoPublico: unico en BD (respaldo final contra duplicados) e inmutable una vez guardado.
            builder.Property(e => e.CodigoPublico).IsRequired().HasMaxLength(32);
            builder.HasIndex(e => e.CodigoPublico).IsUnique();
            builder.Property(e => e.CodigoPublico).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            builder.HasQueryFilter(e => e.Activo);

            builder.HasOne(e => e.Carrera)
                .WithMany()
                .HasForeignKey(e => e.CarreraId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.CreadoPorRegistrante)
                .WithMany()
                .HasForeignKey(e => e.CreadoPorRegistranteId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(e => e.MaestroEncargado)
                .WithMany()
                .HasForeignKey(e => e.MaestroEncargadoId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
