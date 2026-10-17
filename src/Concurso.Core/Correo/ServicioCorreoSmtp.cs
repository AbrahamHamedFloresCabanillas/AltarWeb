using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Concurso.Core.Correo
{
    // Envio SMTP autenticado configurado en la seccion "Smtp" de cada app (Host, Port, EnableSsl, User,
    // Password, FromName). Usar el puerto 587 con STARTTLS: System.Net.Mail.SmtpClient no soporta SSL
    // implicito (465).
    public class ServicioCorreoSmtp
    {
        private readonly IConfiguration _configuration;
        private readonly string _nombreRemitentePorDefecto;

        public ServicioCorreoSmtp(IConfiguration configuration, string nombreRemitentePorDefecto)
        {
            _configuration = configuration;
            _nombreRemitentePorDefecto = nombreRemitentePorDefecto;
        }

        public async Task EnviarAsync(string destino, string asunto, string cuerpo, byte[] adjunto, string nombreArchivo)
        {
            var host = _configuration["Smtp:Host"];
            var user = _configuration["Smtp:User"];
            var password = _configuration["Smtp:Password"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)
                || host == "pendiente-configurar" || user == "pendiente-configurar")
            {
                throw new InvalidOperationException("La configuración SMTP no está completa. Configura la sección \"Smtp\" en appsettings.json.");
            }

            using var smtp = new SmtpClient(host, _configuration.GetValue("Smtp:Port", 587))
            {
                EnableSsl = _configuration.GetValue("Smtp:EnableSsl", true),
                Credentials = new NetworkCredential(user, password)
            };

            using var mail = new MailMessage();
            mail.From = new MailAddress(user, _configuration["Smtp:FromName"] ?? _nombreRemitentePorDefecto);
            mail.To.Add(destino);
            mail.Subject = asunto;
            mail.Body = cuerpo;

            using var stream = new MemoryStream(adjunto);
            mail.Attachments.Add(new Attachment(stream, nombreArchivo, "application/pdf"));
            await smtp.SendMailAsync(mail);
        }
    }
}
