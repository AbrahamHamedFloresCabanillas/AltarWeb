using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Altares.Tests.Integracion;

// SEC-04 (evolucion 2026-10-17): "por cuenta estricto + por IP amplio". La IP de la conexion se simula
// con la cabecera de pruebas X-Test-Remote-IP (ver AltarWebFactory); X-Forwarded-For es lo que un
// cliente cualquiera puede enviar y solo se acepta desde proxies configurados (CFG-02).
public static class Login
{
    public static async Task<string> TokenAsync(HttpClient cliente)
    {
        var html = await cliente.GetStringAsync("/Registro/Login");
        return Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    public static Task<HttpResponseMessage> JuezAsync(HttpClient cliente, string token, string ip, string usuario,
        string password = "incorrecta", string? forwardedFor = null) =>
        Enviar(cliente, "/Acceso/Login", ip, forwardedFor, ("usuario", usuario), ("password", password), ("__RequestVerificationToken", token));

    public static Task<HttpResponseMessage> RegistranteAsync(HttpClient cliente, string token, string ip, string correo, string password = "incorrecta") =>
        Enviar(cliente, "/Registro/Login", ip, null, ("CorreoInstitucional", correo), ("Password", password), ("__RequestVerificationToken", token));

    public static Task<HttpResponseMessage> GoogleAsync(HttpClient cliente, string ip)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Get, "/Registro/GoogleLogin?intent=registrante");
        mensaje.Headers.Add(AltarWebFactory.CabeceraIpRemota, ip);
        return cliente.SendAsync(mensaje);
    }

    private static Task<HttpResponseMessage> Enviar(HttpClient cliente, string ruta, string ip, string? forwardedFor, params (string Clave, string Valor)[] campos)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta)
        {
            Content = new FormUrlEncodedContent(campos.ToDictionary(c => c.Clave, c => c.Valor))
        };
        mensaje.Headers.Add(AltarWebFactory.CabeceraIpRemota, ip);
        if (forwardedFor != null) mensaje.Headers.Add("X-Forwarded-For", forwardedFor);
        return cliente.SendAsync(mensaje);
    }
}

// App con la configuracion POR DEFECTO (5 fallidos / 5 min por cuenta, 300/min login y 600/min OAuth por IP).
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class RateLimitingPorDefectoTests
{
    private readonly AltarWebFactory _factory;

    public RateLimitingPorDefectoTests(AltarWebFactory factory) => _factory = factory;

    // Escenario 3 — NAT de la FIM: cientos de personas distintas desde la misma IP publica. Con el limite
    // anterior (10/min por IP) el 11.o intento se rechazaba aunque cada persona intentara una vez.
    [Fact]
    public async Task CientosDeCuentasDesdeLaMismaIp_NoChocanConUnLimiteDe10PorMinuto()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 200; i++)
        {
            var respuesta = await Login.JuezAsync(cliente, token, "198.51.100.1", $"juez-nat-{i}");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
        }
    }

    // Escenario 1 + 8: los intentos 1-5 se procesan; el 6.o y siguientes reciben 429 con Retry-After
    // dentro de la ventana y una pagina legible.
    [Fact]
    public async Task SextoIntentoFallido_BloqueaLaCuenta_ConRetryAfterYMensaje()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Login.JuezAsync(cliente, token, "198.51.100.2", "cuenta-por-defecto")).StatusCode);
        }

        for (var i = 0; i < 3; i++)
        {
            var bloqueada = await Login.JuezAsync(cliente, token, "198.51.100.2", "cuenta-por-defecto");
            Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
            var espera = bloqueada.Headers.RetryAfter?.Delta;
            Assert.NotNull(espera);
            Assert.InRange(espera!.Value.TotalSeconds, 1, 300);
            Assert.StartsWith("text/html", bloqueada.Content.Headers.ContentType!.ToString());
            Assert.Contains("Demasiados intentos", await bloqueada.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task FlujoGoogle_NoUsaElLimiteDelLoginLocal()
    {
        var cliente = _factory.CrearCliente();

        for (var i = 0; i < 40; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Login.GoogleAsync(cliente, "198.51.100.3")).StatusCode);
        }
    }

    // Escenario 9: ni la contraseña intentada ni el token antiforgery aparecen en ningun log.
    [Fact]
    public async Task LosLogsNoContienenPasswordsNiTokens()
    {
        const string secreto = "Secreto-Que-No-Debe-Loguearse-7f3a";
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 7; i++) await Login.JuezAsync(cliente, token, "198.51.100.4", "cuenta-logs", secreto);
        for (var i = 0; i < 7; i++) await Login.RegistranteAsync(cliente, token, "198.51.100.4", "logs@uabc.edu.mx", secreto);

        var logs = _factory.Logs.ToArray();
        Assert.Contains(logs, l => l.Contains("cuenta-logs"));
        Assert.DoesNotContain(logs, l => l.Contains(secreto));
        Assert.DoesNotContain(logs, l => l.Contains(token));
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

        for (var i = 0; i < 3; i++) await Login.JuezAsync(cliente, token, "198.51.100.10", "cuenta-configurada");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login.JuezAsync(cliente, token, "198.51.100.10", "cuenta-configurada")).StatusCode);
    }

    // Escenarios 2 y 4.
    [Fact]
    public async Task CuentaAtacadaDesdeVariasIps_SigueLimitada_YOtraCuentaDeLaMismaIpPuedeEntrar()
    {
        await _factory.CrearJuezAsync("victima", "Victima-Real-2026!");
        await _factory.CrearJuezAsync("vecino", "Vecino-Real-2026!");
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        // Cada intento desde una IP distinta: el contador es de la cuenta, no de la IP.
        for (var i = 0; i < 3; i++) await Login.JuezAsync(cliente, token, $"198.51.100.{20 + i}", "victima");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login.JuezAsync(cliente, token, "198.51.100.30", "victima")).StatusCode);
        // En pausa incluso con la contraseña correcta.
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.JuezAsync(cliente, token, "198.51.100.31", "victima", "Victima-Real-2026!")).StatusCode);

        // Otra cuenta desde la IP del ataque sigue pudiendo iniciar sesion.
        var vecino = await Login.JuezAsync(cliente, token, "198.51.100.30", "vecino", "Vecino-Real-2026!");
        Assert.Equal(HttpStatusCode.Redirect, vecino.StatusCode);
        Assert.EndsWith("/AltarEvaluacion/Historial", vecino.Headers.Location!.ToString());
    }

    // Bypass por variantes de escritura: mayusculas y espacios cuentan como la misma cuenta.
    [Fact]
    public async Task VariantesDeCasingYEspacios_CompartenContador()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        await Login.JuezAsync(cliente, token, "198.51.100.40", "Cuenta-Variante");
        await Login.JuezAsync(cliente, token, "198.51.100.40", "  cuenta-variante ");
        await Login.JuezAsync(cliente, token, "198.51.100.40", "CUENTA-VARIANTE");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login.JuezAsync(cliente, token, "198.51.100.40", "cuenta-variante")).StatusCode);
    }

    // Un juez puede entrar por usuario o por correo: alternar entre ambos no duplica los intentos.
    [Fact]
    public async Task AlternarUsuarioYCorreoDelMismoJuez_NoDuplicaIntentos()
    {
        await _factory.CrearJuezAsync("juez-alterna", "Alterna-2026!", "juez.alterna@uabc.edu.mx");
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        await Login.JuezAsync(cliente, token, "198.51.100.41", "juez-alterna");
        await Login.JuezAsync(cliente, token, "198.51.100.41", "juez.alterna@uabc.edu.mx");
        await Login.JuezAsync(cliente, token, "198.51.100.41", "juez-alterna");

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.JuezAsync(cliente, token, "198.51.100.41", "juez.alterna@uabc.edu.mx")).StatusCode);
    }

    // Escenario 6: el login correcto reinicia SOLO el contador de esa cuenta.
    [Fact]
    public async Task LoginExitoso_ReiniciaSoloSuPropiaCuenta()
    {
        await _factory.CrearJuezAsync("propia", "Propia-2026!");
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        // Otra cuenta queda bloqueada.
        for (var i = 0; i < 3; i++) await Login.JuezAsync(cliente, token, "198.51.100.50", "ajena");

        // La cuenta propia: 2 fallidos, login correcto, 2 fallidos mas -> no se bloquea (se reinicio).
        for (var i = 0; i < 2; i++) await Login.JuezAsync(cliente, token, "198.51.100.50", "propia");
        Assert.Equal(HttpStatusCode.Redirect, (await Login.JuezAsync(cliente, token, "198.51.100.50", "propia", "Propia-2026!")).StatusCode);
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Login.JuezAsync(cliente, token, "198.51.100.50", "propia")).StatusCode);
        }

        // El login correcto de "propia" no desbloqueo "ajena".
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login.JuezAsync(cliente, token, "198.51.100.50", "ajena")).StatusCode);
    }

    [Fact]
    public async Task LoginDeRegistrante_TambienSeLimitaPorCuenta()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await Login.RegistranteAsync(cliente, token, "198.51.100.60", "alguien@uabc.edu.mx")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.RegistranteAsync(cliente, token, "198.51.100.61", "  ALGUIEN@uabc.edu.mx ")).StatusCode);
    }

    // Escenario 5: el techo por IP existe ante abuso masivo, es por IP y trae Retry-After.
    [Fact]
    public async Task TechoAntiabusoPorIp_Existe()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 25; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Login.JuezAsync(cliente, token, "198.51.100.70", $"masiva-{i}")).StatusCode);
        }

        var rechazada = await Login.JuezAsync(cliente, token, "198.51.100.70", "masiva-26");
        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        Assert.InRange(rechazada.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Login.JuezAsync(cliente, token, "198.51.100.71", "masiva-27")).StatusCode);
    }

    // CFG-02: sin proxies configurados, X-Forwarded-For de un cliente se ignora; rotarlo no evade el techo.
    [Fact]
    public async Task XForwardedForFalsificado_NoEvadeElTechoPorIp()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 25; i++)
        {
            await Login.JuezAsync(cliente, token, "198.51.100.80", $"spoof-{i}", forwardedFor: $"192.0.2.{i + 1}");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.JuezAsync(cliente, token, "198.51.100.80", "spoof-final", forwardedFor: "192.0.2.200")).StatusCode);
    }

    [Fact]
    public async Task TechoDelFlujoGoogle_Existe()
    {
        var cliente = _factory.CrearCliente();

        for (var i = 0; i < 15; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Login.GoogleAsync(cliente, "198.51.100.90")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login.GoogleAsync(cliente, "198.51.100.90")).StatusCode);
    }
}

// Proxy de confianza configurado: solo entonces X-Forwarded-For determina la IP del cliente.
public class AltarWebFactoryConProxy : AltarWebFactory
{
    public const string Proxy = "10.99.0.1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seguridad:ForwardedHeaders:KnownProxies:0"] = Proxy,
            ["Seguridad:RateLimiting:IpLoginPorMinuto"] = "5"
        }));
    }
}

[Trait("Category", "Integracion")]
public class ForwardedHeadersTests : IClassFixture<AltarWebFactoryConProxy>
{
    private readonly AltarWebFactoryConProxy _factory;

    public ForwardedHeadersTests(AltarWebFactoryConProxy factory) => _factory = factory;

    [Fact]
    public async Task DesdeProxyConfiable_CadaClienteTieneSuPropioTecho()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        // 10 clientes distintos detras del proxy: ninguno excede su techo de 5.
        for (var i = 0; i < 10; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests,
                (await Login.JuezAsync(cliente, token, AltarWebFactoryConProxy.Proxy, $"tras-proxy-{i}", forwardedFor: $"192.0.2.{i + 1}")).StatusCode);
        }

        // El mismo cliente real (misma X-Forwarded-For) si alcanza su techo.
        for (var i = 0; i < 4; i++) await Login.JuezAsync(cliente, token, AltarWebFactoryConProxy.Proxy, $"mismo-{i}", forwardedFor: "192.0.2.1");
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.JuezAsync(cliente, token, AltarWebFactoryConProxy.Proxy, "mismo-final", forwardedFor: "192.0.2.1")).StatusCode);
    }

    // ForwardLimit = 1: solo cuenta la entrada mas a la derecha (la que agrego el proxy conocido); lo que
    // el cliente antepone no cambia su IP.
    [Fact]
    public async Task ForwardLimit1_IgnoraEntradasAntepuestasPorElCliente()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 5; i++)
        {
            await Login.JuezAsync(cliente, token, AltarWebFactoryConProxy.Proxy, $"cadena-{i}", forwardedFor: $"203.0.113.{i + 1}, 192.0.2.50");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.JuezAsync(cliente, token, AltarWebFactoryConProxy.Proxy, "cadena-final", forwardedFor: "203.0.113.99, 192.0.2.50")).StatusCode);
    }

    // Un origen que NO es el proxy configurado no puede falsificar su IP.
    [Fact]
    public async Task OrigenNoConfiable_NoPuedeFalsificarSuIp()
    {
        var cliente = _factory.CrearCliente();
        var token = await Login.TokenAsync(cliente);

        for (var i = 0; i < 5; i++)
        {
            await Login.JuezAsync(cliente, token, "198.51.100.120", $"falso-{i}", forwardedFor: $"192.0.2.{100 + i}");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Login.JuezAsync(cliente, token, "198.51.100.120", "falso-final", forwardedFor: "192.0.2.199")).StatusCode);
    }
}
