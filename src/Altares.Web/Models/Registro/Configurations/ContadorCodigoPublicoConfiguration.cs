using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class ContadorCodigoPublicoConfiguration : IEntityTypeConfiguration<ContadorCodigoPublico>
    {
        public void Configure(EntityTypeBuilder<ContadorCodigoPublico> builder)
        {
            builder.ToTable("ContadoresCodigoPublico");
            builder.HasKey(c => c.Periodo);
            builder.Property(c => c.Periodo).HasMaxLength(10);
        }
    }
}
