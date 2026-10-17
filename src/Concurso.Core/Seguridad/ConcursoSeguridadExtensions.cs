using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        public static IServiceCollection AddConcursoSesion(this IServiceCollection services, IConfiguration configuration)
        {
            var nombreCookie = configuration["Seguridad:CookieSesion"];
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
        public static IServiceCollection AddConcursoDataProtection(this IServiceCollection services, IConfiguration configuration)
        {
            var builder = services.AddDataProtection();
            var nombreApp = configuration["Seguridad:DataProtection:ApplicationName"];
            var rutaLlaves = configuration["Seguridad:DataProtection:KeysPath"];
            if (!string.IsNullOrWhiteSpace(nombreApp)) builder.SetApplicationName(nombreApp);
            if (!string.IsNullOrWhiteSpace(rutaLlaves)) builder.PersistKeysToFileSystem(new DirectoryInfo(rutaLlaves));
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
            // CFG-02: en Azure App Service / IIS el TLS puede terminarse en un proxy; sin esto,
            // Request.IsHttps es false detras del proxy, lo que rompe cookies Secure, UseHttpsRedirection
            // y las URLs absolutas del callback de Google. KnownIPNetworks/KnownProxies se vacian porque
            // el hosting no expone un proxy identificable de antemano.
            var forwardedHeadersOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };
            forwardedHeadersOptions.KnownIPNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();
            app.UseForwardedHeaders(forwardedHeadersOptions);

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
