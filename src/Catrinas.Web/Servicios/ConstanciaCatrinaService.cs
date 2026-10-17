using System.IO.Compression;
using Catrinas.Web.Modelos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Catrinas.Web
{
    // Constancias del Concurso de Catrinas (ADR §7.1 (7, 8)): grupal, individual (Personificador o
    // Integrante), maestro encargado (si existe) y juez, con el lugar cuando aplica. Todo sale del
    // snapshot de la evaluacion: no depende de registros vivos ni de Altares.
    public class ConstanciaCatrinaService
    {
        private readonly IWebHostEnvironment _host;
        private readonly ServicioCorreoSmtp _correo;

        public ConstanciaCatrinaService(IWebHostEnvironment host, ServicioCorreoSmtp correo)
        {
            _host = host;
            _correo = correo;
            ComponentesConstancia.Inicializar();
        }

        public byte[] GenerarGrupal(EvaluacionCatrina e) =>
            Generar(e, $"A LA INSCRIPCIÓN: \"{e.SnapshotNombreInscripcion.ToUpperInvariant()}\"",
                $"Personificación de la Catrina a cargo de {e.SnapshotPersonificador}");

        public byte[] GenerarIndividual(EvaluacionCatrina e, IntegranteEvaluado integrante) =>
            Generar(e, $"A: \"{integrante.NombreCompleto.ToUpperInvariant()}\"",
                integrante.Rol == RolInscripcion.Personificador
                    ? $"Por personificar a la Catrina de la inscripción {e.SnapshotNombreInscripcion}"
                    : $"Integrante de la inscripción {e.SnapshotNombreInscripcion}");

        public byte[] GenerarMaestro(EvaluacionCatrina e, string nombreMaestro) =>
            Generar(e, $"AL MAESTRO ENCARGADO: \"{nombreMaestro.ToUpperInvariant()}\"",
                $"Maestro encargado de la inscripción {e.SnapshotNombreInscripcion}");

        public byte[] GenerarJuez(EvaluacionCatrina e, string nombreJuez) =>
            Generar(e, $"AL JURADO: \"{nombreJuez.ToUpperInvariant()}\"",
                $"Por su participación como jurado evaluando a la inscripción {e.SnapshotNombreInscripcion}", esJurado: true);

        public byte[] GenerarZipIndividuales(EvaluacionCatrina e)
        {
            using var memoria = new MemoryStream();
            using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
            {
                foreach (var integrante in e.Integrantes.OrderBy(i => i.Rol == RolInscripcion.Personificador ? 0 : 1).ThenBy(i => i.NombreCompleto))
                {
                    var entrada = zip.CreateEntry(ComponentesConstancia.NombreArchivoSeguro($"Constancia_{integrante.NombreCompleto}.pdf"), CompressionLevel.Fastest);
                    using var flujo = entrada.Open();
                    flujo.Write(GenerarIndividual(e, integrante));
                }
            }
            return memoria.ToArray();
        }

        public static string NombreArchivo(string prefijo, string nombre) =>
            ComponentesConstancia.NombreArchivoSeguro($"{prefijo}_{nombre}.pdf");

        // Envio por correo: grupal al responsable, individual a cada integrante, la del maestro y la del juez.
        public async Task EnviarAsync(EvaluacionCatrina e, string? correoResponsable, string? nombreMaestro, string? correoMaestro,
            string? nombreJuez, string? correoJuez)
        {
            if (!string.IsNullOrWhiteSpace(correoResponsable))
            {
                await _correo.EnviarAsync(correoResponsable, $"Constancia grupal: {e.SnapshotNombreInscripcion}",
                    $"Hola,\n\nAdjuntamos la constancia grupal de la inscripción {e.SnapshotNombreInscripcion} en el Concurso de Catrinas.",
                    GenerarGrupal(e), NombreArchivo("Constancia_Grupal", e.SnapshotNombreInscripcion));
            }

            foreach (var integrante in e.Integrantes.Where(i => !string.IsNullOrWhiteSpace(i.CorreoInstitucional)))
            {
                await _correo.EnviarAsync(integrante.CorreoInstitucional!, $"Constancia: {e.SnapshotNombreInscripcion}",
                    $"Hola {integrante.NombreCompleto},\n\nAdjuntamos tu constancia de participación en el Concurso de Catrinas.",
                    GenerarIndividual(e, integrante), NombreArchivo("Constancia", integrante.NombreCompleto));
            }

            if (!string.IsNullOrWhiteSpace(nombreMaestro) && !string.IsNullOrWhiteSpace(correoMaestro))
            {
                await _correo.EnviarAsync(correoMaestro, $"Constancia de maestro encargado: {e.SnapshotNombreInscripcion}",
                    $"Hola {nombreMaestro},\n\nAdjuntamos tu constancia como maestro encargado.",
                    GenerarMaestro(e, nombreMaestro), NombreArchivo("Constancia_Maestro", nombreMaestro));
            }

            if (!string.IsNullOrWhiteSpace(nombreJuez) && !string.IsNullOrWhiteSpace(correoJuez))
            {
                await _correo.EnviarAsync(correoJuez, $"Constancia de jurado: {e.SnapshotNombreInscripcion}",
                    $"Hola {nombreJuez},\n\nAdjuntamos tu constancia por tu participación como jurado del Concurso de Catrinas.",
                    GenerarJuez(e, nombreJuez), NombreArchivo("Constancia_Jurado", nombreJuez));
            }
        }

        private byte[] Generar(EvaluacionCatrina e, string destinatario, string subtexto, bool esJurado = false)
        {
            string Imagen(string archivo) => Path.Combine(_host.WebRootPath, "images", archivo);
            var lugarTexto = esJurado ? null : e.Lugar switch
            {
                1 => "1.er lugar",
                2 => "2.º lugar",
                3 => "3.er lugar",
                _ => null
            };

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    ComponentesConstancia.ConfigurarPagina(page, Imagen("catrina.png"));
                    page.Content().Column(col =>
                    {
                        col.Item().Element(c => ComponentesConstancia.Encabezado(c, Imagen("logo_uabc.png"), Imagen("logo_fim.png"), Imagen("logo_apfi.png")));
                        col.Item().Height(0.5f, Unit.Centimetre);
                        col.Item().PaddingHorizontal(4.0f, Unit.Centimetre).Column(texto =>
                        {
                            texto.Item().AlignCenter().Text("OTORGA LA PRESENTE").FontSize(12).LetterSpacing(0.2f);
                            texto.Item().AlignCenter().Text("CONSTANCIA DE PARTICIPACIÓN").Bold().FontSize(24).FontColor(Colors.Black);
                            texto.Item().Height(0.5f, Unit.Centimetre);
                            texto.Item().AlignCenter().Text(destinatario).Bold().FontSize(22).FontColor(ComponentesConstancia.Dorado);
                            texto.Item().AlignCenter().Text(subtexto).FontSize(11).FontColor(Colors.Grey.Darken2);
                            if (lugarTexto != null)
                            {
                                texto.Item().AlignCenter().PaddingTop(4).Text($"{lugarTexto} — {e.SnapshotCarrera}").Bold().FontSize(12).FontColor(ComponentesConstancia.VerdeUabc);
                            }
                            texto.Item().Height(0.5f, Unit.Centimetre);
                            texto.Item().Text(t =>
                            {
                                t.Justify();
                                t.DefaultTextStyle(x => x.FontSize(12));
                                t.Span(esJurado
                                    ? "Por su valiosa participación como jurado del "
                                    : "Por su valiosa participación y creatividad en el ");
                                t.Span($"Concurso de Catrinas {e.Periodo}").Bold();
                                t.Span(", realizado en el marco de las celebraciones culturales de la Facultad de Ingeniería de la Universidad Autónoma de Baja California.");
                                t.EmptyLine();
                                t.Span("Su compromiso y entusiasmo contribuyen al fortalecimiento de la identidad universitaria y a la preservación de nuestras tradiciones mexicanas.");
                            });
                            texto.Item().Height(0.5f, Unit.Centimetre);
                            ComponentesConstancia.FechaYFirmas(texto, FechaConcursoHelper.AhoraLocal);
                        });
                    });
                });
            }).GeneratePdf();
        }
    }
}
