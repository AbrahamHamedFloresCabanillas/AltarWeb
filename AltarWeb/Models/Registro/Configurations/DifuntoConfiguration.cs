using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class DifuntoConfiguration : IEntityTypeConfiguration<Difunto>
    {
        public void Configure(EntityTypeBuilder<Difunto> builder)
        {
            builder.ToTable("Difuntos");

            builder.Property(d => d.TipoAltar).HasConversion<string>().HasMaxLength(20);

            builder.HasIndex(d => d.EquipoId).IsUnique();

            builder.HasQueryFilter(d => d.Equipo.Activo);

            builder.HasOne(d => d.Equipo)
                .WithOne(e => e.Difunto)
                .HasForeignKey<Difunto>(d => d.EquipoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
