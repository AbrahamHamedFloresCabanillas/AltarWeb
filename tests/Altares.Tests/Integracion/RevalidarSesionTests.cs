using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Altares.Tests.Integracion;

// M2 (revision de seguridad F3) portado a Altares: dar de baja, dejar pendiente o quitar el rol de admin
// surte efecto en la siguiente peticion de una sesion ya abierta.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class RevalidarSesionTests
{
    private readonly AltarWebFactory _factory;
    public RevalidarSesionTests(AltarWebFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAsync(string usuario, string password)
    {
        var cliente = _factory.CrearCliente();
        var html = await cliente.GetStringAsync("/Registro/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var r = await cliente.PostAsync("/Acceso/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["usuario"] = usuario, ["password"] = password, ["__RequestVerificationToken"] = token
        }));
        Assert.Contains("Historial", r.Headers.Location!.OriginalString);
        return cliente;
    }

    private async Task CambiarJuezAsync(string usuario, Action<Juez> cambio)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
        cambio(await context.Jueces.IgnoreQueryFilters().SingleAsync(j => j.Usuario == usuario));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task JuezDadoDeBaja_PierdeLaSesionAbierta()
    {
        await _factory.CrearJuezAsync("juez-baja-m2", "Juez-Baja-2026!");
        var cliente = await LoginAsync("juez-baja-m2", "Juez-Baja-2026!");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/AltarEvaluacion/Historial")).StatusCode);

        await CambiarJuezAsync("juez-baja-m2", j => { j.IsDeleted = true; j.FechaEliminado = DateTime.Now; });

        Assert.Equal(HttpStatusCode.Redirect, (await cliente.GetAsync("/AltarEvaluacion/Historial")).StatusCode);
        // Reactivar no revive la sesion que ya se limpio.
        await CambiarJuezAsync("juez-baja-m2", j => j.IsDeleted = false);
        Assert.Equal(HttpStatusCode.Redirect, (await cliente.GetAsync("/AltarEvaluacion/Historial")).StatusCode);
    }

    [Fact]
    public async Task AdminSinRol_YaNoAdministra()
    {
        await _factory.CrearJuezAsync("admin-degradado-m2", "Admin-Degradado-2026!", rol: "Admin");
        var cliente = await LoginAsync("admin-degradado-m2", "Admin-Degradado-2026!");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/AltarAdmin/Jueces")).StatusCode);

        await CambiarJuezAsync("admin-degradado-m2", j => j.Rol = "Juez");

        Assert.Equal(HttpStatusCode.Redirect, (await cliente.GetAsync("/AltarAdmin/Jueces")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/AltarEvaluacion/Historial")).StatusCode);
    }
}
