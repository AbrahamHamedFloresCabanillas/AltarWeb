using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class EquipoConfiguration : IEntityTypeConfiguration<Equipo>
    {
        public void Configure(EntityTypeBuilder<Equipo> builder)
        {
            builder.ToTable("EquiposConcurso");

builder.HasIndex(e => new { e.Nombre, e.Periodo }).IsUnique();

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
