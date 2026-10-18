using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Concurso.Core.Seguridad
{
    // Controles de seguridad transversales (hallazgos de AUDITORIA_SEGURIDAD.md) que ambas apps deben
    // aplicar de forma identica. Cada app llama a estos metodos desde su Program.cs.
    public static class ConcursoSeguridadExtensions
    {
        // SEC-05: la cookie de sesion es el portador real de identidad (JuezId/JuezRol/RegistranteId);
        // sin Secure viaja en claro ante un downgrade HTTP y puede ser secuestrada.
        // "Seguridad:CookieSesion" permite darle un nombre distinto por app (Altares y Catrinas pueden
        // compartir dominio padre); sin valor se conserva el nombre por defecto de ASP.NET Core.
        public static IServiceCollection AddConcursoSesion(this IServiceCollection services, IConfiguration configuration,
            string? nombreCookiePorDefecto = null)
        {
            var nombreCookie = configuration["Seguridad:CookieSesion"] ?? nombreCookiePorDefecto;
            return services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(60);
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.IsEssential = true;
                if (!string.IsNullOrWhiteSpace(nombreCookie)) options.Cookie.Name = nombreCookie;
            });
        }

        // Llaves de Data Protection (cifran la cookie de sesion y los tokens antiforgery). Sin
        // configuracion se conserva el comportamiento por defecto de ASP.NET Core. En hosting (IIS sin
        // perfil de usuario) conviene fijar "Seguridad:DataProtection:KeysPath" fuera de wwwroot para
        // que las sesiones sobrevivan a un reciclaje, y un ApplicationName distinto por app.
        public static IServiceCollection AddConcursoDataProtection(this IServiceCollection services, IConfiguration configuration,
            string? applicationNamePorDefecto = null)
        {
            var builder = services.AddDataProtection();
            var nombreApp = configuration["Seguridad:DataProtection:ApplicationName"] ?? applicationNamePorDefecto;
            var rutaLlaves = configuration["Seguridad:DataProtection:KeysPath"];
            if (!string.IsNullOrWhiteSpace(nombreApp)) builder.SetApplicationName(nombreApp);
            // Una ruta relativa (p. ej. "App_Data/keys") se resuelve contra la carpeta de la app y no contra el
            // directorio de trabajo, que en IIS/hosting compartido no siempre es el mismo.
            if (!string.IsNullOrWhiteSpace(rutaLlaves))
            {
                builder.PersistKeysToFileSystem(new DirectoryInfo(Path.IsPathRooted(rutaLlaves) ? rutaLlaves : Path.Combine(AppContext.BaseDirectory, rutaLlaves)));
            }
            return services;
        }

        // SEC-10: valida el antiforgery token en todos los POST/PUT/DELETE.
        // SEC-03: fuerza el cambio de contraseña de cuentas marcadas antes de cualquier accion.
        public static void AgregarFiltrosSeguridad(this MvcOptions options)
        {
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            options.Filters.Add<ForzarCambioPasswordFilter>();
        }

        // CFG-02, CFG-01 y SEC-05 (cabeceras). Debe ir al inicio del pipeline, antes de
        // UseHttpsRedirection/UseStaticFiles.
        public static WebApplication UseConcursoSeguridad(this WebApplication app, string rutaManejadorErrores)
        {
            // CFG-02 (revision 2026-10-17): X-Forwarded-For/Proto solo se aceptan de proxies conocidos
            // (ProxiesConfiablesOptions). Por defecto, solo loopback, el default de ASP.NET Core: cubre
            // desarrollo local, IIS in-process (no hay proxy: IIS entrega la IP y el esquema reales) e IIS
            // out-of-process (ANCM reenvia desde localhost). Antes se vaciaban KnownProxies/KnownNetworks,
            // lo que permitia a cualquier cliente falsificar RemoteIpAddress con X-Forwarded-For y evadir
            // el techo de rate limiting por IP.
            app.UseForwardedHeaders(ProxiesConfiables.CrearOpciones(app.Configuration, app.Logger));

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler(rutaManejadorErrores);
                app.UseHsts();
            }

            // SEC-05: cabeceras de seguridad basicas (defensa en profundidad).
            // DEUDA TECNICA (registrada 2026-07-04, no bloqueante): script-src usa 'unsafe-inline' porque
            // los layouts tienen <script> inline (tema, sidebar, datepicker). Cerrarlo exige mover esos
            // scripts a archivos estaticos o usar nonces por request.
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline'; " +
                    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com; " +
                    "font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; " +
                    "img-src 'self' data:; " +
                    "connect-src 'self';";
                await next();
            });

            return app;
        }
    }
}
