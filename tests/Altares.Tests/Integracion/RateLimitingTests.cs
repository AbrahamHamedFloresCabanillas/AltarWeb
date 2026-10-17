using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Altares.Tests.Integracion;

// SEC-04 (evolucion 2026-10-17): "por cuenta estricto + por IP amplio". La IP de cada peticion se
// simula con X-Forwarded-For (la app confia en ForwardedHeaders, CFG-02).
public static class Login
{
    public static async Task<string> TokenAsync(HttpClient cliente)
    {
        var html = await cliente.GetStringAsync("/Registro/Login");
        return Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    public static Task<HttpResponseMessage> JuezAsync(HttpClient cliente, string token, string ip, string usuario, string password = "incorrecta") =>
        Enviar(cliente, "/Acceso/Login", ip, ("usuario", usuario), ("password", password), ("__RequestVerificationToken", token));

    public static Task<HttpResponseMessage> RegistranteAsync(HttpClient cliente, string token, string ip, string correo) =>
        Enviar(cliente, "/Registro/Login", ip, ("CorreoInstitucional", correo), ("Password", "incorrecta"), ("__RequestVerificationToken", token));

    public static Task<HttpResponseMessage> GoogleAsync(HttpClient cliente, string ip)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Get, "/Registro/GoogleLogin?intent=registrante");
        mensaje.Headers.Add("X-Forwarded-For", ip);
        return cliente.SendAsync(mensaje);
    }

    private static Task<HttpResponseMessage> Enviar(HttpClient cliente, string ruta, string ip, params (string Clave, string Valor)[] campos)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta)
        {
            Content = new FormUrlEncodedContent(campos.ToDictionary(c => c.Clave, c => c.Valor))
        };
        mensaje.Headers.Add("X-Forwarded-For", ip);
        return cliente.SendAsync(mensaje);
    }
}

// App con la configuracion POR DEFECTO (5 fallidos / 5 min por cuenta, 300/min por IP).
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class RateLimitingPorDefectoTests
{
    private readonly AltarWebFactory _factory;

    public RateLimitingPorDefectoTests(AltarWebFactory factory) => _factory = factory;

    // Escenario NAT de la FIM: muchas personas distintas desde la misma IP publica. Con el limite
    // anterior (10/min por IP) el 11.o intento se rechazaba aunque cada persona intentara una vez.
    [Fact]
    public async Task MuchasCuentasDesdeLaMismaIp_NoChocanConUnLimiteDe10PorMinuto()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 60; i++)
        {
            var respuesta = await Login.JuezAsync(cliente, token, "10.20.0.1", $"juez-nat-{i}");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
        }
    }

    [Fact]
    public async Task CuentaIndividual_QuedaLimitadaTras5Fallidos()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Login.JuezAsync(cliente, token, "10.20.0.2", "cuenta-por-defecto")).StatusCode);
        }

        var bloqueada = await Login.JuezAsync(cliente, token, "10.20.0.2", "cuenta-por-defecto");
        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
        Assert.NotNull(bloqueada.Headers.RetryAfter);
        Assert.Contains("Demasiados intentos", await bloqueada.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FlujoGoogle_NoUsaElLimiteDelLoginLocal()
    {
        var cliente = _factory.CrearCliente();

        for (var i = 0; i < 40; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Login.GoogleAsync(cliente, "10.20.0.3")).StatusCode);
        }
    }
}

// App con limites cambiados SOLO por configuracion (sin recompilar): 3 fallidos por cuenta, techo de
// 25/min por IP para login y 15/min para el flujo de Google.
public class AltarWebFactoryConLimites : AltarWebFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seguridad:RateLimiting:CuentaMaxFallidos"] = "3",
            ["Seguridad:RateLimiting:IpLoginPorMinuto"] = "25",
            ["Seguridad:RateLimiting:IpOAuthPorMinuto"] = "15"
        }));
    }
}

[Trait("Category", "Integracion")]
public class RateLimitingConfiguradoTests : IClassFixture<AltarWebFactoryConLimites>
{
    private readonly AltarWebFactoryConLimites _factory;

    public RateLimitingConfiguradoTests(AltarWebFactoryConLimites factory) => _factory = factory;

    // La configuracion (3 en vez de 5) se aplica sin recompilar.
    [Fact]
    public async Task LimiteDeCuentaConfigurado_SeAplica()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 3; i++) await Login.JuezAsync(cliente, token, "10.30.0.1", "cuenta-configurada");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login.JuezAsync(cliente, token, "10.30.0.1", "cuenta-configurada")).StatusCode);
    }

    [Fact]
    public async Task CuentaAtacadaDesdeOtraIp_SigueProtegida_YOtraCuentaDeLaMismaIpSigue()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        var victima = AltarWebFactory.AdminUsuario;
        for (var i = 0; i < 3; i++) await Login.JuezAsync(cliente, token, "10.30.0.2", victima);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login.JuezAsync(cliente, token, "10.30.0.2", victima)).StatusCode);
        // Desde otra IP la cuenta sigue en pausa, ni siquiera con la contraseña correcta.
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.JuezAsync(cliente, token, "10.30.9.9", victima, AltarWebFactory.AdminPassword)).StatusCode);
        // Otra cuenta desde la misma IP puede seguir intentando.
        Assert.Equal(HttpStatusCode.Redirect, (await Login.JuezAsync(cliente, token, "10.30.0.2", "otra-cuenta")).StatusCode);
    }

    [Fact]
    public async Task LoginDeRegistrante_TambienSeLimitaPorCuenta()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await Login.RegistranteAsync(cliente, token, "10.30.0.3", "alguien@uabc.edu.mx")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.RegistranteAsync(cliente, token, "10.30.0.3", "  ALGUIEN@uabc.edu.mx ")).StatusCode);
    }

    [Fact]
    public async Task TechoAntiabusoPorIp_Existe()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 25; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Login.JuezAsync(cliente, token, "10.30.0.4", $"cuenta-{i}")).StatusCode);
        }

        var rechazada = await Login.JuezAsync(cliente, token, "10.30.0.4", "cuenta-26");
        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        Assert.NotNull(rechazada.Headers.RetryAfter);

        // El techo es por IP: otra IP no se ve afectada.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Login.JuezAsync(cliente, token, "10.30.0.5", "cuenta-27")).StatusCode);
    }

    [Fact]
    public async Task TechoDelFlujoGoogle_Existe()
    {
        var cliente = _factory.CrearCliente();

        for (var i = 0; i < 15; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Login.GoogleAsync(cliente, "10.30.0.6")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login.GoogleAsync(cliente, "10.30.0.6")).StatusCode);
    }
}
