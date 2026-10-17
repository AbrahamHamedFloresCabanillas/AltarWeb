using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Concurso.Core.Seguridad
{
    // Seccion "Seguridad:ForwardedHeaders". Ejemplo para un proxy conocido del hosting:
    //   "Seguridad": { "ForwardedHeaders": { "KnownProxies": [ "10.0.0.31" ], "ForwardLimit": 1 } }
    // Las IP/redes reales del proveedor se capturan y verifican en el gate de F4
    // (docs/01-decision-arquitectura.md §10, riesgo R6); no se adivinan.
    public class ProxiesConfiablesOptions
    {
        public const string Seccion = "Seguridad:ForwardedHeaders";

        // IP exactas de proxies de confianza (se suman al loopback por defecto).
        public string[] KnownProxies { get; set; } = Array.Empty<string>();

        // Redes de confianza en notacion CIDR, p. ej. "10.0.0.0/24".
        public string[] KnownNetworks { get; set; } = Array.Empty<string>();

        // Cuantas entradas de X-Forwarded-For se procesan (de derecha a izquierda). 1 = solo el
        // salto inmediato; subirlo solo con una cadena de proxies conocida y demostrada.
        public int ForwardLimit { get; set; } = 1;

        // Equivalente a ASPNETCORE_FORWARDEDHEADERS_ENABLED: confia en cualquier origen. SOLO para
        // plataformas donde la app no es alcanzable mas que a traves del front-end del proveedor
        // (p. ej. Azure App Service Linux) y despues de verificarlo. Nunca por defecto.
        public bool ConfiarEnCualquierOrigen { get; set; }
    }

    public static class ProxiesConfiables
    {
        public static ForwardedHeadersOptions CrearOpciones(IConfiguration configuration, ILogger logger)
        {
            var config = configuration.GetSection(ProxiesConfiablesOptions.Seccion).Get<ProxiesConfiablesOptions>()
                ?? new ProxiesConfiablesOptions();

            var opciones = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                ForwardLimit = Math.Max(1, config.ForwardLimit)
            };

            if (config.ConfiarEnCualquierOrigen)
            {
                opciones.KnownIPNetworks.Clear();
                opciones.KnownProxies.Clear();
                logger.LogWarning("ForwardedHeaders confia en cualquier origen (Seguridad:ForwardedHeaders:ConfiarEnCualquierOrigen=true). " +
                    "Solo es seguro si la app no es alcanzable sin pasar por el proxy del proveedor.");
                return opciones;
            }

            foreach (var proxy in config.KnownProxies.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var ip = IPAddress.Parse(proxy.Trim());
                opciones.KnownProxies.Add(ip);
                // Con sockets dual-mode la IP remota llega como ::ffff:a.b.c.d.
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) opciones.KnownProxies.Add(ip.MapToIPv6());
            }

            foreach (var red in config.KnownNetworks.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                opciones.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(red.Trim()));
            }

            return opciones;
        }
    }
}
