using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Concurso.Core.Pdf
{
    // Manual de usuario: una seccion es un tema ("Crear tu equipo") con texto, pasos numerados y avisos.
    public sealed record SeccionManual(string Titulo, string? Texto = null, IReadOnlyList<string>? Pasos = null, IReadOnlyList<string>? Avisos = null);

    public sealed record ManualUsuario(string Concurso, string Rol, string Introduccion, IReadOnlyList<SeccionManual> Secciones)
    {
        // Nombre de descarga, p. ej. "Manual-Juez-Concurso-de-Altares.pdf". Sin acentos ni espacios: algunos
        // navegadores y sistemas de archivos manejan mal esos caracteres en un nombre de descarga.
        public string NombreArchivo => SinAcentos($"Manual-{Rol}-{Concurso}.pdf".Replace(' ', '-'));

        private static string SinAcentos(string texto) =>
            new(texto.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    // Maquetacion comun de los manuales de usuario de las dos apps (carta vertical, Lato embebida, verde UABC). Solo
    // da formato: el contenido (que se hace en cada pantalla) lo escribe cada aplicacion, asi que aqui no hay reglas
    // de negocio de ningun concurso.
    public static class ManualUsuarioPdf
    {
        public static byte[] Generar(ManualUsuario manual, DateTime fechaEdicion) => Crear(manual, fechaEdicion).GeneratePdf();

        // El documento sin exportar (tambien sirve para renderizarlo a imagenes y revisarlo).
        public static IDocument Crear(ManualUsuario manual, DateTime fechaEdicion)
        {
            ComponentesConstancia.Inicializar();
            var edicion = fechaEdicion.ToString("MMMM 'de' yyyy", new CultureInfo("es-MX"));

            return Document.Create(documento => documento.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(2f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily(ComponentesConstancia.Fuente).FontSize(11).LineHeight(1.35f));

                page.Header().PaddingBottom(10).BorderBottom(1).BorderColor(ComponentesConstancia.VerdeUabc).Row(row =>
                {
                    row.RelativeItem().Text(manual.Concurso).Bold().FontColor(ComponentesConstancia.VerdeUabc);
                    row.RelativeItem().AlignRight().Text($"Manual: {manual.Rol}").FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Manual de usuario — {manual.Rol}").Bold().FontSize(22).FontColor(ComponentesConstancia.VerdeUabc);
                    col.Item().Text($"{manual.Concurso} · Facultad de Ingeniería, UABC · Edición {edicion}").FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().Text(manual.Introduccion);

                    // Indice de contenido.
                    col.Item().PaddingTop(4).Background(Colors.Grey.Lighten4).Padding(10).Column(indice =>
                    {
                        indice.Item().Text("Contenido").Bold();
                        for (var i = 0; i < manual.Secciones.Count; i++)
                        {
                            indice.Item().Text($"{i + 1}. {manual.Secciones[i].Titulo}").FontSize(10);
                        }
                    });

                    for (var i = 0; i < manual.Secciones.Count; i++)
                    {
                        var seccion = manual.Secciones[i];
                        col.Item().PaddingTop(8).Column(s =>
                        {
                            s.Spacing(5);
                            s.Item().Text($"{i + 1}. {seccion.Titulo}").Bold().FontSize(14).FontColor(ComponentesConstancia.VerdeUabc);
                            if (seccion.Texto != null) s.Item().Text(seccion.Texto);
                            if (seccion.Pasos is { Count: > 0 })
                            {
                                for (var p = 0; p < seccion.Pasos.Count; p++)
                                {
                                    var numero = p + 1;
                                    var paso = seccion.Pasos[p];
                                    s.Item().PaddingLeft(6).Row(r =>
                                    {
                                        r.ConstantItem(20).Text($"{numero}.").Bold();
                                        r.RelativeItem().Text(paso);
                                    });
                                }
                            }
                            if (seccion.Avisos is { Count: > 0 })
                            {
                                s.Item().PaddingTop(2).BorderLeft(3).BorderColor(ComponentesConstancia.Dorado)
                                    .Background(Colors.Grey.Lighten5).Padding(8).Column(avisos =>
                                    {
                                        foreach (var aviso in seccion.Avisos)
                                        {
                                            avisos.Item().Text($"• {aviso}").FontSize(10);
                                        }
                                    });
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1));
                    t.Span($"{manual.Concurso} · Manual: {manual.Rol} · Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            }));
        }
    }
}
