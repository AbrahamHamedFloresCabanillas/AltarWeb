using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Concurso.Core.Seguridad
{
    // SEC-04 (evolucion 2026-10-17). Seccion de configuracion "Seguridad:RateLimiting"; todos los
    // valores se pueden cambiar en appsettings.json o por variables de entorno
    // (Seguridad__RateLimiting__CuentaMaxFallidos, etc.) sin recompilar.
    //
    // Diseño: "por cuenta estricto + por IP amplio". La proteccion contra fuerza bruta es el contador de
    // intentos FALLIDOS por cuenta (independiente de la IP, asi que una cuenta atacada desde muchas IP
    // sigue protegida). El limite por IP es solo un techo antiabuso masivo, alto a proposito: en la red
    // de la FIM cientos de personas comparten una IP publica (NAT) y no deben bloquearse entre si.
    public class LimitesLoginOptions
    {
        public const string Seccion = "Seguridad:RateLimiting";

        // Intentos fallidos permitidos por cuenta dentro de la ventana; el siguiente se rechaza.
        public int CuentaMaxFallidos { get; set; } = 5;
        public int CuentaVentanaMinutos { get; set; } = 5;

        // Techo por IP para los POST de login local (Acceso/Login y Registro/Login), por minuto.
        // Valor inicial; el definitivo sale de la prueba de carga (docs/01-decision-arquitectura.md §11).
        public int IpLoginPorMinuto { get; set; } = 300;

        // Techo por IP para el flujo de Google (inicio del challenge, regreso del callback y completar
        // registro), por minuto. Cada persona hace ~3 peticiones de este flujo.
        public int IpOAuthPorMinuto { get; set; } = 600;

        // El identificador intentado se recorta a esta longitud antes de formar la clave.
        public int LongitudMaximaCuenta { get; set; } = 128;

        // Maximo de cuentas rastreadas en memoria a la vez (las entradas expiran con su ventana).
        public int MaxCuentasRastreadas { get; set; } = 50_000;
    }

    public static class PoliticasRateLimit
    {
        public const string LoginIp = "login-ip";
        public const string OAuthIp = "oauth-ip";
    }

    // Contador de intentos fallidos por cuenta (ventana fija desde el primer fallo). Singleton en memoria:
    // con una sola instancia de la app por plataforma no hace falta un almacen distribuido.
    public class LimitadorIntentosLogin : IDisposable
    {
        private sealed class Ventana
        {
            public int Fallidos;
            public DateTimeOffset Fin;
        }

        private readonly IOptionsMonitor<LimitesLoginOptions> _opciones;
        private readonly TimeProvider _reloj;
        private readonly MemoryCache _cache;
        private readonly object _sync = new();

        public LimitadorIntentosLogin(IOptionsMonitor<LimitesLoginOptions> opciones, TimeProvider reloj)
        {
            _opciones = opciones;
            _reloj = reloj;
            _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = Math.Max(1, opciones.CurrentValue.MaxCuentasRastreadas) });
        }

        // Trim + minusculas invariantes + longitud maxima: "  Juan@UABC.edu.mx " y "juan@uabc.edu.mx"
        // comparten contador, y un input arbitrariamente largo no produce claves arbitrariamente largas.
        public string NormalizarCuenta(string? cuenta)
        {
            var normalizada = (cuenta ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizada.Length == 0) return "(vacia)";
            var maximo = Math.Max(1, _opciones.CurrentValue.LongitudMaximaCuenta);
            return normalizada.Length > maximo ? normalizada[..maximo] : normalizada;
        }

        public bool EstaBloqueada(string ambito, string? cuenta, out TimeSpan reintentarEn)
        {
            var opciones = _opciones.CurrentValue;
            var ahora = _reloj.GetUtcNow();
            lock (_sync)
            {
                if (_cache.TryGetValue(Clave(ambito, cuenta), out Ventana? ventana) && ventana != null
                    && ventana.Fin > ahora && ventana.Fallidos >= opciones.CuentaMaxFallidos)
                {
                    reintentarEn = ventana.Fin - ahora;
                    return true;
                }
            }

            reintentarEn = TimeSpan.Zero;
            return false;
        }

        public void RegistrarFallo(string ambito, string? cuenta)
        {
            var opciones = _opciones.CurrentValue;
            var ahora = _reloj.GetUtcNow();
            var clave = Clave(ambito, cuenta);
            lock (_sync)
            {
                if (!_cache.TryGetValue(clave, out Ventana? ventana) || ventana == null || ventana.Fin <= ahora)
                {
                    var duracion = TimeSpan.FromMinutes(Math.Max(1, opciones.CuentaVentanaMinutos));
                    ventana = new Ventana { Fin = ahora + duracion };
                    // La expiracion de la cache solo libera memoria; la regla usa Ventana.Fin.
                    _cache.Set(clave, ventana, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = duracion });
                }

                ventana.Fallidos++;
            }
        }

        // Un login exitoso limpia el contador de esa cuenta.
        public void Reiniciar(string ambito, string? cuenta)
        {
            lock (_sync)
            {
                _cache.Remove(Clave(ambito, cuenta));
            }
        }

        private string Clave(string ambito, string? cuenta) => $"{ambito}|{NormalizarCuenta(cuenta)}";

        public void Dispose() => _cache.Dispose();
    }

    // Respuesta unica para cualquier rechazo por limite: HTTP 429, Retry-After y un mensaje legible.
    public static class RespuestaLimiteExcedido
    {
        public static async Task EscribirAsync(HttpContext context, TimeSpan? reintentarEn)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            var segundos = reintentarEn is { } espera && espera > TimeSpan.Zero ? (int)Math.Ceiling(espera.TotalSeconds) : (int?)null;
            if (segundos != null) context.Response.Headers.RetryAfter = segundos.Value.ToString(CultureInfo.InvariantCulture);
            context.Response.ContentType = "text/html; charset=utf-8";

            var cuando = segundos == null
                ? "en unos momentos"
                : segundos <= 60 ? $"en {segundos} segundos" : $"en {Math.Ceiling(segundos.Value / 60.0)} minutos";
            await context.Response.WriteAsync(
                "<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                "<title>Demasiados intentos</title></head>" +
                "<body style=\"font-family:system-ui,sans-serif;max-width:32rem;margin:4rem auto;padding:0 1rem;line-height:1.5\">" +
                "<h1>Demasiados intentos</h1>" +
                $"<p>Por seguridad pausamos los intentos de inicio de sesión. Vuelve a intentarlo {WebUtility.HtmlEncode(cuando)}.</p>" +
                "<p><a href=\"/\">Volver al inicio</a></p></body></html>");
        }
    }

    public sealed class ResultadoLimiteExcedido : IActionResult
    {
        private readonly TimeSpan _reintentarEn;

        public ResultadoLimiteExcedido(TimeSpan reintentarEn) => _reintentarEn = reintentarEn;

        public Task ExecuteResultAsync(ActionContext context) =>
            RespuestaLimiteExcedido.EscribirAsync(context.HttpContext, _reintentarEn);
    }

    public static class LimitesLoginExtensions
    {
        public static IServiceCollection AddConcursoLimitesLogin(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<LimitesLoginOptions>(configuration.GetSection(LimitesLoginOptions.Seccion));
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<LimitadorIntentosLogin>();

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (contexto, cancelacion) =>
                {
                    var http = contexto.HttpContext;
                    http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Concurso.Core.Seguridad.RateLimiting")
                        .LogWarning("Peticion rechazada por el techo de rate limiting por IP. Ruta: {Ruta}, IP: {IP}",
                            http.Request.Path, http.Connection.RemoteIpAddress);

                    TimeSpan? espera = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : null;
                    await RespuestaLimiteExcedido.EscribirAsync(http, espera);
                };

                options.AddPolicy(PoliticasRateLimit.LoginIp, http => TechoPorIp(http, "login", o => o.IpLoginPorMinuto));
                options.AddPolicy(PoliticasRateLimit.OAuthIp, http => TechoPorIp(http, "oauth", o => o.IpOAuthPorMinuto));
            });

            return services;
        }

        // El limite forma parte de la clave de particion: si se cambia la configuracion en caliente, las
        // particiones nuevas usan el valor nuevo en vez de quedarse con el limitador ya creado.
        private static RateLimitPartition<string> TechoPorIp(HttpContext http, string flujo, Func<LimitesLoginOptions, int> limite)
        {
            var permitidos = Math.Max(1, limite(http.RequestServices.GetRequiredService<IOptionsMonitor<LimitesLoginOptions>>().CurrentValue));
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
            return RateLimitPartition.GetFixedWindowLimiter($"{flujo}|{ip}|{permitidos}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitidos,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        }
    }
}
