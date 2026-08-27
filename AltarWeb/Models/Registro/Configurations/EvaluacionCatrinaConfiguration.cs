using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class EvaluacionCatrinaConfiguration : IEntityTypeConfiguration<EvaluacionCatrina>
    {
        public void Configure(EntityTypeBuilder<EvaluacionCatrina> builder)
        {
            builder.ToTable("EvaluacionesCatrina");

            builder.Property(c => c.Vestuario).HasColumnType("decimal(5,2)");
            builder.Property(c => c.Maquillaje).HasColumnType("decimal(5,2)");
            builder.Property(c => c.Accesorios).HasColumnType("decimal(5,2)");
            builder.Property(c => c.OriginalidadCreatividad).HasColumnType("decimal(5,2)");
            builder.Property(c => c.ApegoTradiciones).HasColumnType("decimal(5,2)");
            builder.Property(c => c.NotaCatrina).HasColumnType("decimal(5,2)");

            builder.HasIndex(c => c.EvaluacionId).IsUnique();

            builder.HasOne(c => c.Evaluacion)
                .WithOne(e => e.EvaluacionCatrina)
                .HasForeignKey<EvaluacionCatrina>(c => c.EvaluacionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
