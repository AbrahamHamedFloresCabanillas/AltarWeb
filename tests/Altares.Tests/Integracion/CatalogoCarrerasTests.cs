using System.Net;
using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Altares.Tests.Integracion;

// B2 (revision manual del propietario, 2026-10-21/22): la carrera Id 2 se llama "Ing. en Bioingeniería" (antes
// "Bioingeniero"). El cambio llega por migracion de datos, asi que tambien corrige las bases ya creadas.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class CatalogoCarrerasTests
{
    private const string Nombre = "Ing. en Bioingeniería";
    private readonly AltarWebFactory _factory;
    public CatalogoCarrerasTests(AltarWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Bioingenieria_TieneElNombreAcordado_EnLaBaseYEnElRegistro()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var carrera = await scope.ServiceProvider.GetRequiredService<AltarDbContext>().CatalogoCarreras.AsNoTracking().SingleAsync(c => c.Id == 2);
            Assert.Equal(Nombre, carrera.Nombre);
        }

        var signup = WebUtility.HtmlDecode(await _factory.CrearCliente().GetStringAsync("/Registro/Signup"));
        Assert.Contains($">{Nombre}<", signup);
        Assert.DoesNotContain(">Bioingeniero<", signup);
    }
}
