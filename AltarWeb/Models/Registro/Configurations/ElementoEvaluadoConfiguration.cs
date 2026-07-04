using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class ElementoEvaluadoConfiguration : IEntityTypeConfiguration<ElementoEvaluado>
    {
        public void Configure(EntityTypeBuilder<ElementoEvaluado> builder)
        {
            builder.ToTable("ElementosEvaluados");

            builder.Property(ee => ee.Satisfaccion).HasConversion<string>().HasMaxLength(20);

            builder.HasIndex(ee => new { ee.EvaluacionId, ee.ElementoId }).IsUnique();

            builder.HasOne(ee => ee.Evaluacion)
                .WithMany(e => e.ElementosEvaluados)
                .HasForeignKey(ee => ee.EvaluacionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ee => ee.Elemento)
                .WithMany()
                .HasForeignKey(ee => ee.ElementoId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
