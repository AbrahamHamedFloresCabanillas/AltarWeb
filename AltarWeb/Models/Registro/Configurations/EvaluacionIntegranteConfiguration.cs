using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class EvaluacionIntegranteConfiguration : IEntityTypeConfiguration<EvaluacionIntegrante>
    {
        public void Configure(EntityTypeBuilder<EvaluacionIntegrante> builder)
        {
            builder.ToTable("EvaluacionIntegrantes");

            builder.Property(ei => ei.Rol).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(ei => ei.Evaluacion)
                .WithMany(e => e.Integrantes)
                .HasForeignKey(ei => ei.EvaluacionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ei => ei.Registrante)
                .WithMany()
                .HasForeignKey(ei => ei.RegistranteId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
