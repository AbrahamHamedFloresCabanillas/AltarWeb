using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class RegistranteEquipoConfiguration : IEntityTypeConfiguration<RegistranteEquipo>
    {
        public void Configure(EntityTypeBuilder<RegistranteEquipo> builder)
        {
            builder.ToTable("RegistranteEquipos");

            builder.HasKey(re => new { re.RegistranteId, re.EquipoId });

            builder.Property(re => re.Rol).HasConversion<string>().HasMaxLength(20);
            builder.Property(re => re.Periodo).IsRequired().HasMaxLength(10);

            // SEC-11: refuerzo a nivel de BD de "un registrante por equipo activo por periodo"
            // (la fila se borra fisicamente al salir de un equipo, ver RegistroController.QuitarIntegrante,
            // asi que un indice unico simple —sin filtro— es suficiente).
            builder.HasIndex(re => new { re.RegistranteId, re.Periodo }).IsUnique();

            builder.HasQueryFilter(re => re.Registrante.Activo && re.Equipo.Activo);

            builder.HasOne(re => re.Registrante)
                .WithMany(r => r.Equipos)
                .HasForeignKey(re => re.RegistranteId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(re => re.Equipo)
                .WithMany(e => e.Integrantes)
                .HasForeignKey(re => re.EquipoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
