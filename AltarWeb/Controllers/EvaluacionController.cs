using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AltarWeb.Controllers
{
    public class EvaluacionController : Controller
    {
        private readonly AltarDbContext _context;
        private readonly IWebHostEnvironment _host;

        public EvaluacionController(AltarDbContext context, IWebHostEnvironment host)
        {
            _context = context;
            _host = host;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public IActionResult Crear()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            return View(new Evaluacion());
        }

        [HttpPost]
        public IActionResult Crear(Evaluacion eval, List<string> NombresIntegrantes, List<string> MatriculasIntegrantes, List<string> CorreosIntegrantes)
        {
            // 1. Asignar Juez
            int? juezId = HttpContext.Session.GetInt32("JuezId");
            if (juezId == null) return RedirectToAction("Login", "Acceso");
            eval.JuezId = juezId.Value;

            ModelState.Remove("Juez");
            ModelState.Remove("JuezId");

            // Helper para recargar integrantes (Optimizado para evitar referencias nulas)
            void RecargarIntegrantes()
            {
                eval.Integrantes = new List<Integrante>();
                if (NombresIntegrantes != null)
                {
                    for (int i = 0; i < NombresIntegrantes.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(NombresIntegrantes[i]))
                        {
                            string matricula = (MatriculasIntegrantes != null && MatriculasIntegrantes.Count > i)
                                ? MatriculasIntegrantes[i]?.Trim() ?? ""
                                : "";

                            string correo = (CorreosIntegrantes != null && CorreosIntegrantes.Count > i)
                                ? CorreosIntegrantes[i]?.Trim() ?? ""
                                : "";

                            eval.Integrantes.Add(new Integrante
                            {
                                Nombre = NombresIntegrantes[i].Trim(),
                                Matricula = matricula,
                                Correo = correo
                            });
                        }
                    }
                }
            }

            // 2. VALIDACIONES
            // A. Nombre de Equipo
            if (_context.Evaluaciones.Any(e => e.NombreEquipo == (eval.NombreEquipo != null ? eval.NombreEquipo.Trim() : "")))
            {
                TempData["Error"] = $"El equipo '{eval.NombreEquipo}' ya existe.";
                RecargarIntegrantes();
                return View(eval);
            }

            // B. Matrículas duplicadas
            if (MatriculasIntegrantes != null)
            {
                var listaLimpia = MatriculasIntegrantes
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m.Trim())
                    .ToList();

                if (listaLimpia.Any(m => !m.All(char.IsDigit)))
                {
                    TempData["Error"] = "Las matrículas deben contener solo números.";
                    RecargarIntegrantes();
                    return View(eval);
                }

                var dups = listaLimpia.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

                if (dups.Count != 0)
                {
                    TempData["Error"] = $"Escribiste la matrícula {dups.First()} dos veces.";
                    RecargarIntegrantes();
                    return View(eval);
                }

                foreach (var mat in listaLimpia)
                {
                    var existe = _context.Integrantes.Include(i => i.Evaluacion).FirstOrDefault(i => i.Matricula == mat);
                    if (existe != null)
                    {
                        string nomEq = existe.Evaluacion?.NombreEquipo ?? "otro equipo";
                        TempData["Error"] = $"El alumno {existe.Nombre} ({mat}) ya está registrado en '{nomEq}'.";
                        RecargarIntegrantes();
                        return View(eval);
                    }
                }
            }

            // 3. CÁLCULOS
            int enc = 0;
            if (eval.ChkFoto) enc++; if (eval.ChkVelas) enc++; if (eval.ChkFlor) enc++; if (eval.ChkPapel) enc++;
            if (eval.ChkPan) enc++; if (eval.ChkAgua) enc++; if (eval.ChkSal) enc++; if (eval.ChkIncienso) enc++;
            if (eval.ChkCalaveritas) enc++; if (eval.ChkObjetos) enc++;

            decimal notaTrad = Math.Min(10, (enc + eval.NotaTradicion) / 2);
            decimal notaPers = Math.Min(10, eval.NotaPersonalizacion + (eval.BonusTematicos * 0.5m));
            eval.NotaFinal = (notaTrad * 0.3m) + (notaPers * 0.4m) + (eval.NotaEstetica * 0.3m);
            eval.Fecha = DateTime.Now;

            RecargarIntegrantes();

            if (eval.Integrantes.Count == 0)
            {
                TempData["Error"] = "Agrega al menos un integrante.";
                return View(eval);
            }

            // 4. GUARDAR
            if (ModelState.IsValid)
            {
                try
                {
                    eval.NombreEquipo = eval.NombreEquipo?.Trim() ?? "Equipo Sin Nombre";

                    _context.Evaluaciones.Add(eval);
                    _context.SaveChanges();

                    if (eval.NotaFinal >= 9.0m)
                    {
                        try { ProcesarEnvioDeCorreos(eval); }
                        catch (Exception ex) { Console.WriteLine("Error Correo: " + ex.Message); }
                    }

                    TempData["Mensaje"] = "Evaluación guardada exitosamente.";
                    return RedirectToAction("Detalle", new { id = eval.Id });
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Error al guardar en BD: " + (ex.InnerException?.Message ?? ex.Message);
                    return View(eval);
                }
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["Error"] = "Faltan datos: " + string.Join(", ", errors);
                return View(eval);
            }
        }

        public IActionResult Detalle(int id)
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            var eval = _context.Evaluaciones.Include(e => e.Integrantes).Include(e => e.Juez).FirstOrDefault(e => e.Id == id);
            if (eval == null) return NotFound();
            if (TempData["Mensaje"] != null) ViewBag.Mensaje = TempData["Mensaje"];
            return View(eval);
        }

        public IActionResult EnviarResultados(int id)
        {
            var eval = _context.Evaluaciones.Include(e => e.Integrantes).FirstOrDefault(e => e.Id == id);
            if (eval != null)
            {
                try
                {
                    bool enviado = ProcesarEnvioDeCorreos(eval);
                    // CAMBIO AQUÍ: Mensajes de texto puro (sin emojis)
                    TempData["Mensaje"] = enviado
                        ? "Correos enviados exitosamente."
                        : "Error de autenticación con el servidor de correo.";
                }
                catch
                {
                    TempData["Mensaje"] = "Ocurrió un error inesperado al intentar enviar.";
                }
            }
            return RedirectToAction("Historial", "Home");
        }

        private bool ProcesarEnvioDeCorreos(Evaluacion eval)
        {
            var pdfBytes = GenerarPdfBytes(eval);

            using var smtp = new SmtpClient("smtp.gmail.com", 587);
            smtp.EnableSsl = true;
            smtp.Credentials = new NetworkCredential(
                "abrahamhamed05@gmail.com",
                "rhbl djvv fyoh slth"
            );

            using var mail = new MailMessage();
            mail.From = new MailAddress("abrahamhamed05@gmail.com", "Concurso Altares FIM");
            mail.Subject = $"Resultados: {eval.NombreEquipo}";
            mail.Body = $"Hola equipo {eval.NombreEquipo},\n\nSu calificación final es: {eval.NotaFinal:F1}/10.\n\nAdjunto encontrarán su Constancia de Participación.\n\n¡Gracias por participar!";

            using var stream = new MemoryStream(pdfBytes);
            mail.Attachments.Add(new Attachment(stream, $"Constancia_{eval.NombreEquipo}.pdf"));

            bool hayDestinatarios = false;
            foreach (var i in eval.Integrantes)
            {
                if (!string.IsNullOrWhiteSpace(i.Correo))
                {
                    mail.To.Add(i.Correo);
                    hayDestinatarios = true;
                }
            }

            if (hayDestinatarios) { smtp.Send(mail); return true; }
            return false;
        }

        private byte[] GenerarPdfBytes(Evaluacion eval)
        {
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

                    if (System.IO.File.Exists(rutaCatrina))
                    {
                        page.Foreground().AlignLeft().AlignBottom().Height(12.5f, Unit.Centimetre).TranslateX(-1.5f, Unit.Centimetre).Image(rutaCatrina).FitArea();
                    }

                    page.Content().Column(col =>
                    {
                        float anchoLogos = 5.5f;
                        col.Item().Row(row =>
                        {
                            row.ConstantItem(anchoLogos, Unit.Centimetre).AlignMiddle().Row(izq => { if (System.IO.File.Exists(rutaUABC)) izq.RelativeItem().AlignCenter().Width(2.3f, Unit.Centimetre).Image(rutaUABC).FitArea(); });
                            row.RelativeItem().PaddingHorizontal(0.5f, Unit.Centimetre).AlignMiddle().Column(header => {
                                header.Item().AlignCenter().Text("UNIVERSIDAD AUTÓNOMA DE BAJA CALIFORNIA").Bold().FontSize(16).FontColor("#00703c");
                                header.Item().AlignCenter().Text("FACULTAD DE INGENIERÍA").Bold().FontSize(14);
                                header.Item().AlignCenter().Text("ASOCIACIÓN DE PROFESORES DE LA FACULTAD DE INGENIERÍA (APFI)").FontSize(10);
                            });
                            row.ConstantItem(anchoLogos, Unit.Centimetre).AlignMiddle().Row(logosDer => {
                                if (System.IO.File.Exists(rutaFIM)) logosDer.RelativeItem(1.0f).PaddingTop(5).PaddingRight(5).Image(rutaFIM).FitArea();
                                if (System.IO.File.Exists(rutaAPFI)) logosDer.RelativeItem(1.5f).Image(rutaAPFI).FitArea();
                            });
                        });
                        col.Item().Height(0.5f, Unit.Centimetre);
                        col.Item().PaddingHorizontal(4.0f, Unit.Centimetre).Column(textoCentral => {
                            textoCentral.Item().AlignCenter().Text("OTORGA LA PRESENTE").FontSize(12).LetterSpacing(0.2f);
                            textoCentral.Item().AlignCenter().Text("CONSTANCIA DE AGRADECIMIENTO").Bold().FontSize(24).FontColor(Colors.Black);
                            textoCentral.Item().Height(0.5f, Unit.Centimetre);
                            textoCentral.Item().AlignCenter().Text($"A EL EQUIPO: \"{eval.NombreEquipo.ToUpper()}\"").Bold().FontSize(22).FontColor("#b08d55");
                            textoCentral.Item().Height(0.5f, Unit.Centimetre);
                            textoCentral.Item().Text(text => {
                                text.Justify(); text.ParagraphSpacing(5); text.DefaultTextStyle(x => x.FontSize(12));
                                text.Span("Por su valiosa participación y creatividad en la elaboración del Altar de Muertos dedicado a ");
                                text.Span($"{eval.NombreDifunto}").Bold();
                                text.Span(", realizado en el marco de las celebraciones culturales de la Facultad de Ingeniería de la Universidad Autónoma de Baja California.");
                                text.EmptyLine();
                                text.Span("Su compromiso y entusiasmo contribuyen al fortalecimiento de la identidad universitaria y a la preservación de nuestras tradiciones mexicanas.");
                            });
                            textoCentral.Item().Height(0.5f, Unit.Centimetre);
                            textoCentral.Item().AlignCenter().Text($"Mexicali, Baja California a {DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-MX"))}").Italic();
                            textoCentral.Item().AlignCenter().Text("\"Por la realización plena del ser\"").Italic().FontSize(9).FontColor(Colors.Grey.Darken2);
                            textoCentral.Item().PaddingTop(0.5f, Unit.Centimetre).Row(row => {
                                row.RelativeItem().Column(firm => { firm.Item().Height(2.5f, Unit.Centimetre); firm.Item().AlignCenter().Width(7, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Black); firm.Item().PaddingTop(5).AlignCenter().Text("Dra. Araceli Celina Justo López").Bold().FontSize(9); firm.Item().AlignCenter().Text("Directora de la Facultad de Ingeniería").FontSize(8); });
                                row.ConstantItem(2.0f, Unit.Centimetre);
                                row.RelativeItem().Column(firm => { firm.Item().Height(2.5f, Unit.Centimetre); firm.Item().AlignCenter().Width(7, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Black); firm.Item().PaddingTop(5).AlignCenter().Text("Ing. María Carmiña Reyes Revelez").Bold().FontSize(9); firm.Item().AlignCenter().Text("Presidenta de la APFI").FontSize(8); });
                            });
                        });
                    });
                });
            });

            return documento.GeneratePdf();
        }
    }
}