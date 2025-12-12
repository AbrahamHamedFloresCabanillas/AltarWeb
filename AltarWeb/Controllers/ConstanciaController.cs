using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AltarWeb.Controllers
{
    public class ConstanciaController : Controller
    {
        private readonly AltarDbContext _context;
        private readonly IWebHostEnvironment _host;

        public ConstanciaController(AltarDbContext context, IWebHostEnvironment host)
        {
            _context = context;
            _host = host;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            var lista = _context.Evaluaciones.OrderByDescending(e => e.Fecha).ToList();
            return View(lista);
        }

        public IActionResult Descargar(int id)
        {
            var eval = _context.Evaluaciones.FirstOrDefault(e => e.Id == id);
            if (eval == null) return NotFound();

            // Rutas de imágenes
            string rutaCatrina = Path.Combine(_host.WebRootPath, "images", "catrina.png");
            string rutaUABC = Path.Combine(_host.WebRootPath, "images", "logo_uabc.png");
            string rutaFIM = Path.Combine(_host.WebRootPath, "images", "logo_fim.png");
            string rutaAPFI = Path.Combine(_host.WebRootPath, "images", "logo_apfi.png");

            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter.Landscape());
                    page.Margin(1.0f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11));

                    // --- 1. FONDO (CATRINA) ---
                    if (System.IO.File.Exists(rutaCatrina))
                    {
                        page.Foreground()
                            .AlignLeft()
                            .AlignBottom()
                            .Height(12.5f, Unit.Centimetre)
                            .TranslateX(-1.5f, Unit.Centimetre)
                            .Image(rutaCatrina)
                            .FitArea();
                    }

                    // --- 2. CONTENIDO PRINCIPAL ---
                    page.Content().Column(col =>
                    {
                        // A. ENCABEZADO
                        float anchoLogos = 5.5f;

                        col.Item().Row(row =>
                        {
                            // 1. COLUMNA IZQUIERDA (UABC)
                            row.ConstantItem(anchoLogos, Unit.Centimetre).AlignMiddle().Row(izq =>
                            {
                                if (System.IO.File.Exists(rutaUABC))
                                    izq.RelativeItem()
                                       .AlignCenter()
                                       .Width(2.3f, Unit.Centimetre)
                                       .Image(rutaUABC).FitArea();
                            });

                            // 2. TEXTO CENTRAL
                            row.RelativeItem().PaddingHorizontal(0.5f, Unit.Centimetre).AlignMiddle().Column(header =>
                            {
                                header.Item().AlignCenter().Text("UNIVERSIDAD AUTÓNOMA DE BAJA CALIFORNIA").Bold().FontSize(16).FontColor("#00703c");
                                header.Item().AlignCenter().Text("FACULTAD DE INGENIERÍA").Bold().FontSize(14);
                                header.Item().AlignCenter().Text("ASOCIACIÓN DE PROFESORES DE LA FACULTAD DE INGENIERÍA (APFI)").FontSize(10);
                            });

                            // 3. COLUMNA DERECHA (FIM y APFI)
                            row.ConstantItem(anchoLogos, Unit.Centimetre).AlignMiddle().Row(logosDer =>
                            {
                                // FIM: Se agregó PaddingTop(5) para bajarlo un poco
                                if (System.IO.File.Exists(rutaFIM))
                                    logosDer.RelativeItem(1.0f)
                                            .PaddingTop(5) // <--- AJUSTE AQUÍ
                                            .PaddingRight(5)
                                            .Image(rutaFIM).FitArea();

                                // APFI
                                if (System.IO.File.Exists(rutaAPFI))
                                    logosDer.RelativeItem(1.5f).Image(rutaAPFI).FitArea();
                            });
                        });

                        col.Item().Height(0.5f, Unit.Centimetre);

                        // --- CONTENEDOR CENTRAL PARA EL TEXTO ---
                        col.Item().PaddingHorizontal(4.0f, Unit.Centimetre).Column(textoCentral =>
                        {
                            // B. TÍTULOS
                            textoCentral.Item().AlignCenter().Text("OTORGA LA PRESENTE").FontSize(12).LetterSpacing(0.2f);
                            textoCentral.Item().AlignCenter().Text("CONSTANCIA DE AGRADECIMIENTO").Bold().FontSize(24).FontColor(Colors.Black);

                            textoCentral.Item().Height(0.5f, Unit.Centimetre);

                            // C. EQUIPO
                            textoCentral.Item().AlignCenter().Text($"A EL EQUIPO: \"{eval.NombreEquipo.ToUpper()}\"").Bold().FontSize(22).FontColor("#b08d55");

                            textoCentral.Item().Height(0.5f, Unit.Centimetre);

                            // D. CUERPO
                            textoCentral.Item()
                               .Text(text =>
                               {
                                   text.Justify();
                                   text.ParagraphSpacing(5);
                                   text.DefaultTextStyle(x => x.FontSize(12));

                                   text.Span("Por su valiosa participación y creatividad en la elaboración del Altar de Muertos dedicado a ");
                                   text.Span($"{eval.NombreDifunto}").Bold();
                                   text.Span(", realizado en el marco de las celebraciones culturales de la Facultad de Ingeniería de la Universidad Autónoma de Baja California.");
                                   text.EmptyLine();
                                   text.Span("Su compromiso y entusiasmo contribuyen al fortalecimiento de la identidad universitaria y a la preservación de nuestras tradiciones mexicanas.");
                               });

                            textoCentral.Item().Height(0.5f, Unit.Centimetre);

                            // E. FECHA
                            textoCentral.Item().AlignCenter().Text($"Mexicali, Baja California a {DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-MX"))}").Italic();
                            textoCentral.Item().AlignCenter().Text("\"Por la realización plena del ser\"").Italic().FontSize(9).FontColor(Colors.Grey.Darken2);

                            // F. FIRMAS
                            textoCentral.Item().PaddingTop(0.5f, Unit.Centimetre).Row(row =>
                            {
                                // --- Firma 1 ---
                                row.RelativeItem().Column(firm =>
                                {
                                    firm.Item().Height(2.5f, Unit.Centimetre);
                                    firm.Item().AlignCenter().Width(7, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Black);
                                    firm.Item().PaddingTop(5).AlignCenter().Text("Dra. Araceli Celina Justo López").Bold().FontSize(9);
                                    firm.Item().AlignCenter().Text("Directora de la Facultad de Ingeniería").FontSize(8);
                                });

                                row.ConstantItem(2.0f, Unit.Centimetre);

                                // --- Firma 2 ---
                                row.RelativeItem().Column(firm =>
                                {
                                    firm.Item().Height(2.5f, Unit.Centimetre);
                                    firm.Item().AlignCenter().Width(7, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Black);
                                    firm.Item().PaddingTop(5).AlignCenter().Text("Ing. María Carmiña Reyes Revelez").Bold().FontSize(9);
                                    firm.Item().AlignCenter().Text("Presidenta de la APFI").FontSize(8);
                                });
                            });
                        });
                    });
                });
            });

            byte[] pdfBytes = documento.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Constancia_{eval.NombreEquipo}.pdf");
        }
    }
}