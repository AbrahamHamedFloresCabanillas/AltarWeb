using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class CatalogoCarreraConfiguration : IEntityTypeConfiguration<CatalogoCarrera>
    {
        public void Configure(EntityTypeBuilder<CatalogoCarrera> builder)
        {
            builder.ToTable("CatalogoCarreras");

            builder.HasIndex(c => c.Nombre).IsUnique();

            // Catalogo oficial FIM, Apendice C de vision.md (confirmado por Salch).
            builder.HasData(
                new CatalogoCarrera { Id = 1, Nombre = "Lic. en Sistemas Computacionales", Orden = 1, Activo = true },
                new CatalogoCarrera { Id = 2, Nombre = "Bioingeniero", Orden = 2, Activo = true },
                new CatalogoCarrera { Id = 3, Nombre = "Ing. Aeroespacial", Orden = 3, Activo = true },
                new CatalogoCarrera { Id = 4, Nombre = "Ing. Civil", Orden = 4, Activo = true },
                new CatalogoCarrera { Id = 5, Nombre = "Ing. en Computación", Orden = 5, Activo = true },
                new CatalogoCarrera { Id = 6, Nombre = "Ing. en Electrónica", Orden = 6, Activo = true },
                new CatalogoCarrera { Id = 7, Nombre = "Ing. Eléctrico", Orden = 7, Activo = true },
                new CatalogoCarrera { Id = 8, Nombre = "Ing. en Energías Renovables", Orden = 8, Activo = true },
                new CatalogoCarrera { Id = 9, Nombre = "Ing. Industrial", Orden = 9, Activo = true },
                new CatalogoCarrera { Id = 10, Nombre = "Ing. Mecánico", Orden = 10, Activo = true },
                new CatalogoCarrera { Id = 11, Nombre = "Ing. en Mecatrónica", Orden = 11, Activo = true },
                new CatalogoCarrera { Id = 12, Nombre = "Ing. en Semiconductores y Microelectrónica", Orden = 12, Activo = true },
                new CatalogoCarrera { Id = 13, Nombre = "Ing. de Datos e Inteligencia Artificial", Orden = 13, Activo = true }
            );
        }
    }
}
