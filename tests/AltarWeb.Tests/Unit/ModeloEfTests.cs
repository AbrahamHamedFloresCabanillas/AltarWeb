using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Tests.Unit;

public class ModeloEfTests
{
    // Desde EF Core 9, Database.Migrate() lanza excepcion si el modelo tiene cambios sin migracion
    // (PendingModelChangesWarning). Esta prueba lo detecta sin necesitar una base de datos.
    [Fact]
    public void ElModeloNoTieneCambiosSinMigracion()
    {
        using var context = new AltarDbContext(new DbContextOptionsBuilder<AltarDbContext>()
            .UseSqlServer("Server=sin-conexion;Database=sin-conexion")
            .Options);

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
