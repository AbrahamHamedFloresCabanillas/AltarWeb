using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class EvaluacionConfiguration : IEntityTypeConfiguration<Evaluacion>
    {
        public void Configure(EntityTypeBuilder<Evaluacion> builder)
        {
            builder.ToTable("EvaluacionesConcurso");

builder.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20);

            builder.Property(e => e.PuntajeElementos).HasColumnType("decimal(5,2)");
            builder.Property(e => e.DistribucionNiveles).HasColumnType("decimal(5,2)");
            builder.Property(e => e.Narrador).HasColumnType("decimal(5,2)");
            builder.Property(e => e.TematicaHobbies).HasColumnType("decimal(5,2)");
            builder.Property(e => e.ObjetivoCultural).HasColumnType("decimal(5,2)");
            builder.Property(e => e.EsenciaPersonalidad).HasColumnType("decimal(5,2)");
            builder.Property(e => e.EsenciaPersonalidadFinal).HasColumnType("decimal(5,2)");
            builder.Property(e => e.ValoracionGeneral).HasColumnType("decimal(5,2)");
            builder.Property(e => e.NotaFinal).HasColumnType("decimal(5,2)");

            builder.HasIndex(e => e.EquipoId).IsUnique();

            builder.HasOne(e => e.Equipo)
                .WithOne(eq => eq.Evaluacion)
                .HasForeignKey<Evaluacion>(e => e.EquipoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.Juez)
                .WithMany()
                .HasForeignKey(e => e.JuezId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
