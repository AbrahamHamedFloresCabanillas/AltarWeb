using System.IO.Compression;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace AltarWeb.Services
{
    public class ConstanciaService
    {
        private readonly IWebHostEnvironment _host;
        private readonly ServicioCorreoSmtp _correo;

        public ConstanciaService(IWebHostEnvironment host, ServicioCorreoSmtp correo)
        {
            _host = host;
            _correo = correo;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerarGrupal(Evaluacion evaluacion)
        {
            return GenerarPdf(evaluacion, $"A EL EQUIPO: \"{evaluacion.SnapshotNombreEquipo.ToUpperInvariant()}\"", null);
        }

        public byte[] GenerarIndividual(Evaluacion evaluacion, string nombreIntegrante)
        {
            return GenerarPdf(
                evaluacion,
                $"A: \"{nombreIntegrante.ToUpperInvariant()}\"",
                $"Participante del equipo: {evaluacion.SnapshotNombreEquipo}");
        }

        public byte[] GenerarMaestroEncargado(Evaluacion evaluacion, string nombreMaestro)
        {
            return GenerarPdf(
                evaluacion,
                $"AL MAESTRO ENCARGADO: \"{nombreMaestro.ToUpperInvariant()}\"",
                $"Maestro encargado del equipo: {evaluacion.SnapshotNombreEquipo}");
        }

        public byte[] GenerarJuez(Evaluacion evaluacion, string nombreJuez)
        {
            return GenerarPdf(
                evaluacion,
                $"AL JUEZ: \"{nombreJuez.ToUpperInvariant()}\"",
                $"Por su participación evaluando al equipo: {evaluacion.SnapshotNombreEquipo}");
        }

        public byte[] GenerarZipIndividuales(Evaluacion evaluacion)
        {
            using var memoria = new MemoryStream();
            using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
            {
                foreach (var integrante in evaluacion.Integrantes)
                {
                    var pdf = GenerarIndividual(evaluacion, integrante.NombreCompleto);
                    var nombreArchivo = LimpiarNombreArchivo($"Constancia_{integrante.NombreCompleto}.pdf");
                    var entry = zip.CreateEntry(nombreArchivo, CompressionLevel.Fastest);
                    using var stream = entry.Open();
                    stream.Write(pdf, 0, pdf.Length);
                }
            }

            return memoria.ToArray();
        }

        public async Task EnviarConstancias(
            Evaluacion evaluacion,
            string? correoCreador,
            string nombreMaestro,
            string? correoMaestro,
            string nombreJuez,
            string? correoJuez)
        {
            if (!string.IsNullOrWhiteSpace(correoCreador))
            {
                await _correo.EnviarAsync(
                    correoCreador,
                    $"Constancia grupal: {evaluacion.SnapshotNombreEquipo}",
                    $"Hola,\n\nAdjuntamos la constancia grupal del equipo {evaluacion.SnapshotNombreEquipo}.",
                    GenerarGrupal(evaluacion),
                    LimpiarNombreArchivo($"Constancia_Grupal_{evaluacion.SnapshotNombreEquipo}.pdf"));
            }

            foreach (var integrante in evaluacion.Integrantes)
            {
                var correo = integrante.Registrante?.CorreoInstitucional;
                if (string.IsNullOrWhiteSpace(correo)) continue;

                await _correo.EnviarAsync(
                    correo,
                    $"Constancia individual: {evaluacion.SnapshotNombreEquipo}",
                    $"Hola {integrante.NombreCompleto},\n\nAdjuntamos tu constancia individual de participación.",
                    GenerarIndividual(evaluacion, integrante.NombreCompleto),
                    LimpiarNombreArchivo($"Constancia_{integrante.NombreCompleto}.pdf"));
            }

            if (!string.IsNullOrWhiteSpace(correoMaestro))
            {
                await _correo.EnviarAsync(
                    correoMaestro,
                    $"Constancia de maestro encargado: {evaluacion.SnapshotNombreEquipo}",
                    $"Hola {nombreMaestro},\n\nAdjuntamos tu constancia como maestro encargado del equipo {evaluacion.SnapshotNombreEquipo}.",
                    GenerarMaestroEncargado(evaluacion, nombreMaestro),
                    LimpiarNombreArchivo($"Constancia_Maestro_{nombreMaestro}.pdf"));
            }

            if (!string.IsNullOrWhiteSpace(correoJuez))
            {
                await _correo.EnviarAsync(
                    correoJuez,
                    $"Constancia de juez: {evaluacion.SnapshotNombreEquipo}",
                    $"Hola {nombreJuez},\n\nAdjuntamos tu constancia por tu participación como juez.",
                    GenerarJuez(evaluacion, nombreJuez),
                    LimpiarNombreArchivo($"Constancia_Juez_{nombreJuez}.pdf"));
            }
        }

        private byte[] GenerarPdf(Evaluacion evaluacion, string destinatario, string? subtexto)
        {
            string rutaCatrina = Path.Combine(_host.WebRootPath, "images", "catrina.png");
            string rutaUabc = Path.Combine(_host.WebRootPath, "images", "logo_uabc.png");
            string rutaFim = Path.Combine(_host.WebRootPath, "images", "logo_fim.png");
            string rutaApfi = Path.Combine(_host.WebRootPath, "images", "logo_apfi.png");

            var difuntoNombre = evaluacion.SnapshotDifuntoNombre;
            var lugarTexto = evaluacion.Lugar switch
            {
                1 => "1.er lugar",
                2 => "2.o lugar",
                3 => "3.er lugar",
                _ => null
            };

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
                            if (lugarTexto != null)
                            {
                                texto.Item().AlignCenter().PaddingTop(4).Text($"{lugarTexto} — {evaluacion.Equipo?.Carrera?.Nombre}").Bold().FontSize(12).FontColor("#00703c");
                            }
                            texto.Item().Height(0.5f, Unit.Centimetre);
                            texto.Item().Text(text =>
                            {
                                text.Justify();
                                text.ParagraphSpacing(5);
                                text.DefaultTextStyle(x => x.FontSize(12));
                                text.Span("Por su valiosa participacion y creatividad en la elaboracion del Altar de Muertos dedicado a ");
                                text.Span(difuntoNombre).Bold();
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

        // M2 (pruebas adversariales 2026-10-21): un nombre de 5,021 caracteres generaba dentro del ZIP un archivo
        // que Windows no puede extraer. Ademas de limpiar caracteres se recorta, conservando la extension.
        public const int LongitudMaximaNombreArchivo = 120;

        public static string LimpiarNombreArchivo(string valor)
        {
            foreach (var caracter in Path.GetInvalidFileNameChars())
            {
                valor = valor.Replace(caracter, '_');
            }

            if (valor.Length <= LongitudMaximaNombreArchivo) return valor;

            var extension = Path.GetExtension(valor);
            return valor[..(LongitudMaximaNombreArchivo - extension.Length)].TrimEnd() + extension;
        }
    }
}
