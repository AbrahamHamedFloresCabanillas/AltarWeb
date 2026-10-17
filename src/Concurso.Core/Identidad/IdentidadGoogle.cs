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

        public static AuthenticationBuilder AddGoogleInstitucional(this AuthenticationBuilder builder, IConfiguration configuration)
        {
            return builder.AddGoogle("Google", options =>
            {
                options.ClientId = configuration["GoogleAuth:ClientId"] ?? string.Empty;
                options.ClientSecret = configuration["GoogleAuth:ClientSecret"] ?? string.Empty;
                options.CallbackPath = CallbackPathPorDefecto;
                options.Scope.Add("email");
                options.Scope.Add("profile");
            });
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
