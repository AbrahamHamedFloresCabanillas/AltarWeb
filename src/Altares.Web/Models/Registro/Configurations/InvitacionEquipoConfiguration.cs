using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class InvitacionEquipoConfiguration : IEntityTypeConfiguration<InvitacionEquipo>
    {
        public void Configure(EntityTypeBuilder<InvitacionEquipo> builder)
        {
            builder.ToTable("InvitacionesEquipo");

            builder.Property(i => i.Periodo).IsRequired().HasMaxLength(10);

            // Una sola invitacion pendiente por persona y equipo (reinvitar no duplica).
            builder.HasIndex(i => new { i.EquipoId, i.RegistranteId }).IsUnique();
            builder.HasIndex(i => new { i.RegistranteId, i.Periodo });

            // Mismo filtro que RegistranteEquipo: una invitacion de un equipo o registrante inactivo no se muestra.
            builder.HasQueryFilter(i => i.Registrante.Activo && i.Equipo.Activo);

            // Restrict hacia Registrante (como RegistranteEquipo): con Cascade, SQL Server rechaza la tabla por
            // multiples rutas de cascada (Registrante -> Equipo.CreadoPor -> Equipo -> Invitacion).
            builder.HasOne(i => i.Registrante)
                .WithMany()
                .HasForeignKey(i => i.RegistranteId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(i => i.Equipo)
                .WithMany()
                .HasForeignKey(i => i.EquipoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
