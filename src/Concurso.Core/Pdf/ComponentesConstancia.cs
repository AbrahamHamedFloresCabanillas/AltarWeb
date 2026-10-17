using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Concurso.Core.Pdf
{
    // Primitives institucionales para constancias (encabezado UABC/FIM/APFI, fecha, lema y firmas).
    // Son identicos para cualquier concurso de la FIM; el texto de cada constancia (a quien, por que,
    // lugar) lo pone cada aplicacion. Usa la fuente Lato que QuestPDF trae embebida, asi que no depende
    // de fuentes del sistema operativo (funciona en hosting Windows o Linux).
    public static class ComponentesConstancia
    {
        public const string Fuente = "Lato";
        public const string VerdeUabc = "#00703c";
        public const string Dorado = "#b08d55";

        // QuestPDF exige la licencia ANTES de crear cualquier documento (licencia comunitaria, vision.md §14).
        // Idempotente: cada servicio de constancias la llama al construirse.
        public static void Inicializar() => QuestPDF.Settings.License = LicenseType.Community;

        public static void ConfigurarPagina(PageDescriptor page, string? rutaIlustracion)
        {
            page.Size(PageSizes.Letter.Landscape());
            page.Margin(1.0f, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily(Fuente).FontSize(11));

            if (rutaIlustracion != null && File.Exists(rutaIlustracion))
            {
                page.Foreground().AlignLeft().AlignBottom().Height(12.5f, Unit.Centimetre).TranslateX(-1.5f, Unit.Centimetre).Image(rutaIlustracion).FitArea();
            }
        }

        public static void Encabezado(IContainer container, string rutaLogoUabc, string rutaLogoFim, string rutaLogoApfi)
        {
            container.Row(row =>
            {
                row.ConstantItem(5.5f, Unit.Centimetre).AlignMiddle().Row(izq =>
                {
                    if (File.Exists(rutaLogoUabc)) izq.RelativeItem().AlignCenter().Width(2.3f, Unit.Centimetre).Image(rutaLogoUabc).FitArea();
                });
                row.RelativeItem().PaddingHorizontal(0.5f, Unit.Centimetre).AlignMiddle().Column(header =>
                {
                    header.Item().AlignCenter().Text("UNIVERSIDAD AUTÓNOMA DE BAJA CALIFORNIA").Bold().FontSize(16).FontColor(VerdeUabc);
                    header.Item().AlignCenter().Text("FACULTAD DE INGENIERÍA").Bold().FontSize(14);
                    header.Item().AlignCenter().Text("ASOCIACIÓN DE PROFESORES DE LA FACULTAD DE INGENIERÍA (APFI)").FontSize(10);
                });
                row.ConstantItem(5.5f, Unit.Centimetre).AlignMiddle().Row(der =>
                {
                    if (File.Exists(rutaLogoFim)) der.RelativeItem(1.0f).PaddingTop(5).PaddingRight(5).Image(rutaLogoFim).FitArea();
                    if (File.Exists(rutaLogoApfi)) der.RelativeItem(1.5f).Image(rutaLogoApfi).FitArea();
                });
            });
        }

        public static string FechaMexicali(DateTime fecha) =>
            $"Mexicali, Baja California a {fecha.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("es-MX"))}";

        public static void FechaYFirmas(ColumnDescriptor col, DateTime fecha)
        {
            col.Item().AlignCenter().Text(FechaMexicali(fecha)).Italic();
            col.Item().AlignCenter().Text("\"Por la realización plena del ser\"").Italic().FontSize(9).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(0.5f, Unit.Centimetre).Row(row =>
            {
                Firma(row.RelativeItem(), "Dra. Araceli Celina Justo López", "Directora de la Facultad de Ingeniería");
                row.ConstantItem(2.0f, Unit.Centimetre);
                Firma(row.RelativeItem(), "Ing. María Carmina Reyes Revelez", "Presidenta de la APFI");
            });
        }

        private static void Firma(IContainer container, string nombre, string cargo)
        {
            container.Column(firma =>
            {
                firma.Item().Height(2.5f, Unit.Centimetre);
                firma.Item().AlignCenter().Width(7, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Black);
                firma.Item().PaddingTop(5).AlignCenter().Text(nombre).Bold().FontSize(9);
                firma.Item().AlignCenter().Text(cargo).FontSize(8);
            });
        }

        public static string NombreArchivoSeguro(string valor)
        {
            foreach (var caracter in Path.GetInvalidFileNameChars())
            {
                valor = valor.Replace(caracter, '_');
            }
            return valor;
        }
    }
}
