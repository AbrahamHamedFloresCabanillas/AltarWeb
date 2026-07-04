using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class ConfiguracionPeriodoConfiguration : IEntityTypeConfiguration<ConfiguracionPeriodo>
    {
        public void Configure(EntityTypeBuilder<ConfiguracionPeriodo> builder)
        {
            builder.ToTable("ConfiguracionesPeriodo");

            builder.HasIndex(c => c.Periodo).IsUnique();

            builder.Property(c => c.PesoObjetivoCultural).HasColumnType("decimal(5,4)");
            builder.Property(c => c.PesoEsenciaPersonalidad).HasColumnType("decimal(5,4)");
            builder.Property(c => c.PesoValoracionGeneral).HasColumnType("decimal(5,4)");
            builder.Property(c => c.PesoDistribucionNiveles).HasColumnType("decimal(5,4)");
            builder.Property(c => c.PesoNarrador).HasColumnType("decimal(5,4)");
            builder.Property(c => c.ValorSatisfaccionNoPresente).HasColumnType("decimal(5,4)");
            builder.Property(c => c.ValorSatisfaccionPoco).HasColumnType("decimal(5,4)");
            builder.Property(c => c.ValorSatisfaccionSatisfactorio).HasColumnType("decimal(5,4)");
            builder.Property(c => c.ValorSatisfaccionMuySatisfactorio).HasColumnType("decimal(5,4)");
            builder.Property(c => c.PesoElementoRitual).HasColumnType("decimal(5,4)");
            builder.Property(c => c.PesoElementoDecorativo).HasColumnType("decimal(5,4)");
            builder.Property(c => c.BonusPorElementoTematizado).HasColumnType("decimal(5,4)");

            // Punto de partida editable desde /Admin (seccion 7.7). Periodo actual segun PeriodoHelper al momento de esta migracion.
            builder.HasData(
                new ConfiguracionPeriodo
                {
                    Id = 1,
                    Periodo = "2026-1",
                    FechaLimiteInscripcion = null,
                    FechaLimiteRequisitos = null,
                    RecorridoPdf = null,
                    PesoObjetivoCultural = 0.30m,
                    PesoEsenciaPersonalidad = 0.30m,
                    PesoValoracionGeneral = 0.20m,
                    PesoDistribucionNiveles = 0.10m,
                    PesoNarrador = 0.10m,
                    ValorSatisfaccionNoPresente = 0.0m,
                    ValorSatisfaccionPoco = 0.5m,
                    ValorSatisfaccionSatisfactorio = 0.75m,
                    ValorSatisfaccionMuySatisfactorio = 1.0m,
                    PesoElementoRitual = 1.0m,
                    PesoElementoDecorativo = 0.5m,
                    BonusPorElementoTematizado = 0.25m,
                    ExigirMinimoUnAnioFallecimiento = true,
                    UmbralAgrupacionDemografica = 5
                }
            );
        }
    }
}
