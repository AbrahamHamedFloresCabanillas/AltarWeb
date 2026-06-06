using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using AltarWeb.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AltarWeb.Services
{
    public class ConstanciaService
    {
        private readonly IWebHostEnvironment _host;
        private readonly IConfiguration _configuration;

        public ConstanciaService(IWebHostEnvironment host, IConfiguration configuration)
        {
            _host = host;
            _configuration = configuration;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerarGrupal(Evaluacion evaluacion, string nombreEquipo)
        {
            return GenerarPdf(evaluacion, $"A EL EQUIPO: \"{nombreEquipo.ToUpperInvariant()}\"", null);
        }

        public byte[] GenerarIndividual(Evaluacion evaluacion, string nombreIntegrante, string nombreEquipo)
        {
            return GenerarPdf(
                evaluacion,
                $"A: \"{nombreIntegrante.ToUpperInvariant()}\"",
                $"Participante del equipo: {nombreEquipo}");
        }

        public byte[] GenerarZipIndividuales(Evaluacion evaluacion, IEnumerable<AlumnoEquipo> integrantes)
        {
            using var memoria = new MemoryStream();
            using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
            {
                foreach (var integrante in integrantes.Where(i => i.Alumno != null))
                {
                    var nombreEquipo = evaluacion.ObtenerNombreEquipo();
                    var pdf = GenerarIndividual(evaluacion, integrante.Alumno.NombreCompleto, nombreEquipo);
                    var nombreArchivo = LimpiarNombreArchivo($"Constancia_{integrante.Alumno.NombreCompleto}.pdf");
                    var entry = zip.CreateEntry(nombreArchivo, CompressionLevel.Fastest);
                    using var stream = entry.Open();
                    stream.Write(pdf, 0, pdf.Length);
                }
            }

            return memoria.ToArray();
        }

        public async Task EnviarConstancias(Evaluacion evaluacion, List<AlumnoEquipo> integrantes)
        {
            var nombreEquipo = evaluacion.ObtenerNombreEquipo();
            var creador = integrantes.FirstOrDefault(i => i.EsCreador)?.Alumno
                ?? integrantes.FirstOrDefault(i => i.AlumnoId == evaluacion.Equipo?.CreadoPorAlumnoId)?.Alumno;

            if (creador != null && !string.IsNullOrWhiteSpace(creador.CorreoElectronico))
            {
                await EnviarCorreoAsync(
                    creador.CorreoElectronico,
                    $"Constancia grupal: {nombreEquipo}",
                    $"Hola {creador.NombreCompleto},\n\nAdjuntamos la constancia grupal del equipo {nombreEquipo}.",
                    GenerarGrupal(evaluacion, nombreEquipo),
                    LimpiarNombreArchivo($"Constancia_Grupal_{nombreEquipo}.pdf"));
            }

            foreach (var integrante in integrantes.Select(i => i.Alumno).Where(a => a != null))
            {
                if (string.IsNullOrWhiteSpace(integrante.CorreoElectronico)) continue;

                await EnviarCorreoAsync(
                    integrante.CorreoElectronico,
                    $"Constancia individual: {nombreEquipo}",
                    $"Hola {integrante.NombreCompleto},\n\nAdjuntamos tu constancia individual de participacion.",
                    GenerarIndividual(evaluacion, integrante.NombreCompleto, nombreEquipo),
                    LimpiarNombreArchivo($"Constancia_{integrante.NombreCompleto}.pdf"));
            }
        }

        private async Task EnviarCorreoAsync(string destino, string asunto, string cuerpo, byte[] adjunto, string nombreArchivo)
        {
            var host = _configuration["Smtp:Host"];
            var user = _configuration["Smtp:User"];
            var password = _configuration["Smtp:Password"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("La configuracion SMTP esta incompleta.");
            }

            using var smtp = new SmtpClient(host, _configuration.GetValue("Smtp:Port", 587))
            {
                EnableSsl = _configuration.GetValue("Smtp:EnableSsl", true),
                Credentials = new NetworkCredential(user, password)
            };

            using var mail = new MailMessage();
            mail.From = new MailAddress(user, _configuration["Smtp:FromName"] ?? "Concurso Altares FIM");
            mail.To.Add(destino);
            mail.Subject = asunto;
            mail.Body = cuerpo;

            using var stream = new MemoryStream(adjunto);
            mail.Attachments.Add(new Attachment(stream, nombreArchivo, "application/pdf"));
            await smtp.SendMailAsync(mail);
        }

        private byte[] GenerarPdf(Evaluacion evaluacion, string destinatario, string? subtexto)
        {
            string rutaCatrina = Path.Combine(_host.WebRootPath, "images", "catrina.png");
            string rutaUabc = Path.Combine(_host.WebRootPath, "images", "logo_uabc.png");
            string rutaFim = Path.Combine(_host.WebRootPath, "images", "logo_fim.png");
            string rutaApfi = Path.Combine(_host.WebRootPath, "images", "logo_apfi.png");

            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter.Landscape());
                    page.Margin(1.0f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11));

                    if (File.Exists(rutaCatrina))
                    {
                        page.Foreground().AlignLeft().AlignBottom().Height(12.5f, Unit.Centimetre).TranslateX(-1.5f, Unit.Centimetre).Image(rutaCatrina).FitArea();
                    }

                    page.Content().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.ConstantItem(5.5f, Unit.Centimetre).AlignMiddle().Row(izq =>
                            {
                                if (File.Exists(rutaUabc)) izq.RelativeItem().AlignCenter().Width(2.3f, Unit.Centimetre).Image(rutaUabc).FitArea();
                            });
                            row.RelativeItem().PaddingHorizontal(0.5f, Unit.Centimetre).AlignMiddle().Column(header =>
                            {
                                header.Item().AlignCenter().Text("UNIVERSIDAD AUTONOMA DE BAJA CALIFORNIA").Bold().FontSize(16).FontColor("#00703c");
                                header.Item().AlignCenter().Text("FACULTAD DE INGENIERIA").Bold().FontSize(14);
                                header.Item().AlignCenter().Text("ASOCIACION DE PROFESORES DE LA FACULTAD DE INGENIERIA (APFI)").FontSize(10);
                            });
                            row.ConstantItem(5.5f, Unit.Centimetre).AlignMiddle().Row(logosDer =>
                            {
                                if (File.Exists(rutaFim)) logosDer.RelativeItem(1.0f).PaddingTop(5).PaddingRight(5).Image(rutaFim).FitArea();
                                if (File.Exists(rutaApfi)) logosDer.RelativeItem(1.5f).Image(rutaApfi).FitArea();
                            });
                        });

                        col.Item().Height(0.5f, Unit.Centimetre);
                        col.Item().PaddingHorizontal(4.0f, Unit.Centimetre).Column(texto =>
                        {
                            texto.Item().AlignCenter().Text("OTORGA LA PRESENTE").FontSize(12).LetterSpacing(0.2f);
                            texto.Item().AlignCenter().Text("CONSTANCIA DE AGRADECIMIENTO").Bold().FontSize(24).FontColor(Colors.Black);
                            texto.Item().Height(0.5f, Unit.Centimetre);
                            texto.Item().AlignCenter().Text(destinatario).Bold().FontSize(22).FontColor("#b08d55");
                            if (!string.IsNullOrWhiteSpace(subtexto))
                            {
                                texto.Item().AlignCenter().Text(subtexto).FontSize(11).FontColor(Colors.Grey.Darken2);
                            }
                            texto.Item().Height(0.5f, Unit.Centimetre);
                            texto.Item().Text(text =>
                            {
                                text.Justify();
                                text.ParagraphSpacing(5);
                                text.DefaultTextStyle(x => x.FontSize(12));
                                text.Span("Por su valiosa participacion y creatividad en la elaboracion del Altar de Muertos dedicado a ");
                                text.Span(evaluacion.NombreDifunto).Bold();
                                text.Span(", realizado en el marco de las celebraciones culturales de la Facultad de Ingenieria de la Universidad Autonoma de Baja California.");
                                text.EmptyLine();
                                text.Span("Su compromiso y entusiasmo contribuyen al fortalecimiento de la identidad universitaria y a la preservacion de nuestras tradiciones mexicanas.");
                            });
                            texto.Item().Height(0.5f, Unit.Centimetre);
                            texto.Item().AlignCenter().Text($"Mexicali, Baja California a {DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-MX"))}").Italic();
                            texto.Item().AlignCenter().Text("\"Por la realizacion plena del ser\"").Italic().FontSize(9).FontColor(Colors.Grey.Darken2);
                            texto.Item().PaddingTop(0.5f, Unit.Centimetre).Row(row =>
                            {
                                row.RelativeItem().Column(firm =>
                                {
                                    firm.Item().Height(2.5f, Unit.Centimetre);
                                    firm.Item().AlignCenter().Width(7, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Black);
                                    firm.Item().PaddingTop(5).AlignCenter().Text("Dra. Araceli Celina Justo Lopez").Bold().FontSize(9);
                                    firm.Item().AlignCenter().Text("Directora de la Facultad de Ingenieria").FontSize(8);
                                });
                                row.ConstantItem(2.0f, Unit.Centimetre);
                                row.RelativeItem().Column(firm =>
                                {
                                    firm.Item().Height(2.5f, Unit.Centimetre);
                                    firm.Item().AlignCenter().Width(7, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Black);
                                    firm.Item().PaddingTop(5).AlignCenter().Text("Ing. Maria Carmina Reyes Revelez").Bold().FontSize(9);
                                    firm.Item().AlignCenter().Text("Presidenta de la APFI").FontSize(8);
                                });
                            });
                        });
                    });
                });
            });

            return documento.GeneratePdf();
        }

        private static string LimpiarNombreArchivo(string valor)
        {
            foreach (var caracter in Path.GetInvalidFileNameChars())
            {
                valor = valor.Replace(caracter, '_');
            }

            return valor;
        }
    }
}
