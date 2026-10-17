using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Concurso.Core.Identidad
{
    public static class DominioInstitucional
    {
        public const string Sufijo = "@uabc.edu.mx";

        public static bool EsCorreoInstitucional(string? correo) =>
            !string.IsNullOrWhiteSpace(correo) && correo.Trim().EndsWith(Sufijo, StringComparison.OrdinalIgnoreCase);
    }

    public static class GoogleInstitucionalExtensions
    {
        // Callback que consume el middleware de Google. La ruta a la que se redirige DESPUES del login
        // debe ser distinta (p. ej. /Registro/google-listo), o el middleware intentaria procesarla otra vez.
        public const string CallbackPathPorDefecto = "/Registro/google-callback";

        public const string ClaimEmailVerificado = "email_verified";
        public const string ClaimDominioWorkspace = "hd";

        public static AuthenticationBuilder AddGoogleInstitucional(this AuthenticationBuilder builder, IConfiguration configuration)
        {
            builder.Services.Configure<ValidacionGoogleOptions>(configuration.GetSection(ValidacionGoogleOptions.Seccion));
            return builder.AddGoogle("Google", options =>
            {
                options.ClientId = configuration["GoogleAuth:ClientId"] ?? string.Empty;
                options.ClientSecret = configuration["GoogleAuth:ClientSecret"] ?? string.Empty;
                options.CallbackPath = CallbackPathPorDefecto;
                options.Scope.Add("email");
                options.Scope.Add("profile");

                // SEC-01 (endurecimiento 2026-10-17): el handler de Google solo mapea por defecto sub,
                // nombre y correo. Se agregan "email_verified" y "hd" de la respuesta de userinfo para que
                // ValidacionIdentidadGoogle pueda exigirlos server-side.
                options.ClaimActions.MapJsonKey(ClaimEmailVerificado, ClaimEmailVerificado);
                options.ClaimActions.MapJsonKey(ClaimDominioWorkspace, ClaimDominioWorkspace);
            });
        }
    }

    // Seccion "GoogleAuth" (ademas de ClientId/ClientSecret).
    public class ValidacionGoogleOptions
    {
        public const string Seccion = "GoogleAuth";

        // Exige que la cuenta pertenezca a la organizacion de Google Workspace de la UABC (claim "hd").
        // Google indica que el dominio del correo NO basta para identificar cuentas de una organizacion;
        // el claim "hd" del token es la fuente confiable. Se puede desactivar por configuracion solo si en
        // la verificacion de F4 resulta que cuentas UABC legitimas no traen "hd".
        public bool ExigirDominioWorkspace { get; set; } = true;

        public string DominioWorkspace { get; set; } = DominioInstitucional.Sufijo.TrimStart('@');
    }

    public enum MotivoRechazoGoogle
    {
        Ninguno,
        SinCorreo,
        DominioNoInstitucional,
        CorreoNoVerificado,
        DominioWorkspaceInvalido
    }

    public sealed record ResultadoIdentidadGoogle(bool Valida, string Email, string Nombre, MotivoRechazoGoogle Motivo);

    // Validacion server-side de la identidad que entrega Google, antes de usarla para iniciar sesion o
    // para guardar la identidad verificada de un solo uso. Solo usa claims del ticket emitido tras el
    // intercambio de codigo con Google (nunca valores del cliente).
    public static class ValidacionIdentidadGoogle
    {
        public static ResultadoIdentidadGoogle Validar(ClaimsPrincipal principal, ValidacionGoogleOptions opciones)
        {
            var email = principal.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant() ?? string.Empty;
            var nombre = principal.FindFirstValue(ClaimTypes.Name)?.Trim();
            if (string.IsNullOrEmpty(nombre)) nombre = string.IsNullOrEmpty(email) ? "Participante" : email;

            if (email.Length == 0) return Rechazo(MotivoRechazoGoogle.SinCorreo);
            if (!DominioInstitucional.EsCorreoInstitucional(email)) return Rechazo(MotivoRechazoGoogle.DominioNoInstitucional);

            var verificado = principal.FindFirstValue(GoogleInstitucionalExtensions.ClaimEmailVerificado)?.Trim();
            if (!string.Equals(verificado, "true", StringComparison.OrdinalIgnoreCase)) return Rechazo(MotivoRechazoGoogle.CorreoNoVerificado);

            if (opciones.ExigirDominioWorkspace)
            {
                var hd = principal.FindFirstValue(GoogleInstitucionalExtensions.ClaimDominioWorkspace)?.Trim();
                if (!string.Equals(hd, opciones.DominioWorkspace.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return Rechazo(MotivoRechazoGoogle.DominioWorkspaceInvalido);
                }
            }

            return new ResultadoIdentidadGoogle(true, email, nombre, MotivoRechazoGoogle.Ninguno);

            static ResultadoIdentidadGoogle Rechazo(MotivoRechazoGoogle motivo) => new(false, string.Empty, string.Empty, motivo);
        }
    }

    // SEC-01: el email/nombre que muestran las vistas de "completar registro" viajan como campo oculto,
    // asi que no son confiables en el POST (un atacante puede postear cualquier valor sin haber pasado
    // por Google). Se guardan aqui, server-side, justo despues de validar el ticket de Google, y los POST
    // leen exclusivamente de esta fuente. Token de un solo uso de vida corta (10 minutos) atado al intent.
    public static class IdentidadGoogleVerificada
    {
        private const string ClaveIntent = "GooglePendienteIntent";
        private const string ClaveEmail = "GooglePendienteEmail";
        private const string ClaveNombre = "GooglePendienteNombre";
        private const string ClaveExpira = "GooglePendienteExpira";
        private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(10);

        public static void Guardar(ISession session, string intent, string email, string nombre)
        {
            session.SetString(ClaveIntent, intent);
            session.SetString(ClaveEmail, email);
            session.SetString(ClaveNombre, nombre);
            session.SetString(ClaveExpira, DateTime.UtcNow.Add(Vigencia).ToString("O"));
        }

        // Exige que el intent coincida con el formulario que se esta procesando y que no hayan pasado
        // mas de 10 minutos desde el callback de Google.
        public static (bool Valido, string Email, string Nombre) Obtener(ISession session, string intentEsperado)
        {
            var intent = session.GetString(ClaveIntent);
            var email = session.GetString(ClaveEmail);
            var nombre = session.GetString(ClaveNombre);
            var expiraRaw = session.GetString(ClaveExpira);

            if (intent != intentEsperado || string.IsNullOrEmpty(email)
                || !DateTime.TryParse(expiraRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expira)
                || DateTime.UtcNow > expira)
            {
                return (false, string.Empty, string.Empty);
            }

            return (true, email, nombre ?? email);
        }

        public static void Limpiar(ISession session)
        {
            session.Remove(ClaveIntent);
            session.Remove(ClaveEmail);
            session.Remove(ClaveNombre);
            session.Remove(ClaveExpira);
        }
    }
}
