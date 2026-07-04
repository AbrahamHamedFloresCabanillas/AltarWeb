using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class CatalogoGeneroConfiguration : IEntityTypeConfiguration<CatalogoGenero>
    {
        public void Configure(EntityTypeBuilder<CatalogoGenero> builder)
        {
            builder.ToTable("CatalogoGeneros");

            builder.HasIndex(g => g.Nombre).IsUnique();

            // Propuesta editable del Apendice B de vision.md; el admin puede agregar/desactivar opciones.
            builder.HasData(
                new CatalogoGenero { Id = 1, Nombre = "Masculino", Orden = 1, Activo = true },
                new CatalogoGenero { Id = 2, Nombre = "Femenino", Orden = 2, Activo = true },
                new CatalogoGenero { Id = 3, Nombre = "No binario", Orden = 3, Activo = true },
                new CatalogoGenero { Id = 4, Nombre = "Prefiero no especificar", Orden = 4, Activo = true },
                new CatalogoGenero { Id = 5, Nombre = "Otro", Orden = 5, Activo = true }
            );
        }
    }
}
