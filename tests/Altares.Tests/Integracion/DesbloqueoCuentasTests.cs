using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using AltarWeb.Models.Registro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Altares.Tests.Integracion;

// A3 (pruebas adversariales 2026-10-21): cualquiera puede bloquear 5 minutos la cuenta de otra persona con 5
// contraseñas incorrectas (p. ej. a un juez el dia del concurso). Decision del propietario: los limites NO se
// relajan (5 fallidos / 5 min por cuenta); la administracion ve que cuentas estan bloqueadas y puede desbloquearlas.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class DesbloqueoCuentasTests
{
    private const string Password = "Desbloqueo-Cuentas-2026!";
    private readonly AltarWebFactory _factory;
    public DesbloqueoCuentasTests(AltarWebFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminAsync(string usuario)
    {
        await _factory.CrearJuezAsync(usuario, Password, rol: "Admin");
        var admin = _factory.CrearCliente();
        var login = await Login.JuezAsync(admin, await Login.TokenAsync(admin), "203.0.113.90", usuario, Password);
        Assert.Contains("Historial", login.Headers.Location!.OriginalString);
        return admin;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rutaFormulario, string accion, params (string, string)[] campos)
    {
        var html = await cliente.GetStringAsync(rutaFormulario);
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }

    [Fact]
    public async Task JuezBloqueadoPorTerceros_ElAdminLoVeYLoDesbloquea()
    {
        await _factory.CrearJuezAsync("juez-saboteado", Password, correo: "juez.saboteado@uabc.edu.mx");
        var atacante = _factory.CrearCliente();
        for (var i = 0; i < 5; i++) await Login.JuezAsync(atacante, await Login.TokenAsync(atacante), "198.51.100.10", "juez-saboteado");

        var dueno = _factory.CrearCliente();
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.JuezAsync(dueno, await Login.TokenAsync(dueno), "198.51.100.20", "juez-saboteado", Password)).StatusCode);

        var admin = await AdminAsync("admin-desbloqueo-juez");
        var lista = await admin.GetStringAsync("/AltarAdmin/Jueces");
        Assert.Contains("Bloqueada", lista);
        var id = await IdJuezAsync("juez-saboteado");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(admin, "/AltarAdmin/Jueces", "/AltarAdmin/DesbloquearJuez", ("id", id.ToString()))).StatusCode);

        var login = await Login.JuezAsync(dueno, await Login.TokenAsync(dueno), "198.51.100.20", "juez-saboteado", Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("Historial", login.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task RegistranteBloqueadoPorTerceros_ElAdminLoVeYLoDesbloquea()
    {
        const string correo = "registrante.saboteado@uabc.edu.mx";
        var cuenta = _factory.CrearCliente();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(cuenta, "/Registro/Signup", "/Registro/Signup",
            ("NombreCompleto", "Registrante Saboteado"), ("Identificador", "1307701"), ("CorreoInstitucional", correo),
            ("Password", Password), ("ConfirmarPassword", Password))).StatusCode);

        var atacante = _factory.CrearCliente();
        for (var i = 0; i < 5; i++) await Login.RegistranteAsync(atacante, await Login.TokenAsync(atacante), "198.51.100.30", correo);
        var dueno = _factory.CrearCliente();
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.RegistranteAsync(dueno, await Login.TokenAsync(dueno), "198.51.100.40", correo, Password)).StatusCode);

        var admin = await AdminAsync("admin-desbloqueo-reg");
        Assert.Contains("Bloqueada", await admin.GetStringAsync("/AltarAdmin/RegistrantesYEquipos?tab=registrantes"));
        var id = await ConContextoAsync(c => c.Registrantes.Where(r => r.CorreoInstitucional == correo).Select(r => r.Id).SingleAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(admin, "/AltarAdmin/RegistrantesYEquipos?tab=registrantes", "/AltarAdmin/DesbloquearRegistrante", ("id", id.ToString()))).StatusCode);

        var login = await Login.RegistranteAsync(dueno, await Login.TokenAsync(dueno), "198.51.100.40", correo, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("Dashboard", login.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Desbloquear_SoloLoPuedeHacerUnAdmin()
    {
        await _factory.CrearJuezAsync("juez-no-admin-desbloqueo", Password);
        var juez = _factory.CrearCliente();
        var login = await Login.JuezAsync(juez, await Login.TokenAsync(juez), "203.0.113.91", "juez-no-admin-desbloqueo", Password);
        Assert.Contains("Historial", login.Headers.Location?.OriginalString ?? $"(sin redireccion: {(int)login.StatusCode})");
        var id = await IdJuezAsync("juez-no-admin-desbloqueo");

        var respuesta = await PostAsync(juez, "/Registro/Login", "/AltarAdmin/DesbloquearJuez", ("id", id.ToString()));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.DoesNotContain("Jueces", respuesta.Headers.Location!.OriginalString);
    }

    private Task<int> IdJuezAsync(string usuario) => ConContextoAsync(c => c.Jueces.Where(j => j.Usuario == usuario).Select(j => j.Id).SingleAsync());

    private async Task<T> ConContextoAsync<T>(Func<AltarDbContext, Task<T>> accion)
    {
        using var scope = _factory.Services.CreateScope();
        return await accion(scope.ServiceProvider.GetRequiredService<AltarDbContext>());
    }
}
