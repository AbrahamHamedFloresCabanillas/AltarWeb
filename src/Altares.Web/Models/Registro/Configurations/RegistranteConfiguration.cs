using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class RegistranteConfiguration : IEntityTypeConfiguration<Registrante>
    {
        public void Configure(EntityTypeBuilder<Registrante> builder)
        {
            builder.ToTable("Registrantes");

            builder.Property(r => r.Tipo).HasConversion<string>().HasMaxLength(20);

            builder.HasIndex(r => r.Identificador).IsUnique();
            builder.HasIndex(r => r.CorreoInstitucional).IsUnique();

            builder.HasQueryFilter(r => r.Activo);

            builder.HasOne(r => r.CatalogoGenero)
                .WithMany()
                .HasForeignKey(r => r.CatalogoGeneroId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(r => r.CatalogoCarrera)
                .WithMany()
                .HasForeignKey(r => r.CatalogoCarreraId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
