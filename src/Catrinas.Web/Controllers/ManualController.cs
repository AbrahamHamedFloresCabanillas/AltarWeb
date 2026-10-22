using Catrinas.Web.Servicios;
using Concurso.Core.Pdf;
using Microsoft.AspNetCore.Mvc;

namespace Catrinas.Web.Controllers
{
    // Manuales de usuario en PDF, uno por rol (peticion del propietario, 2026-10-22). La pagina "Manuales" del menu
    // lateral es para el jurado y la administracion; el manual del participante es publico (no tiene datos de nadie)
    // para enlazarlo desde su panel; el del jurado pide sesion de jurado y el de administracion, rol Admin.
    [Route("Manual/{action=Index}")]
    public class ManualController : Controller
    {
        [SoloJurado]
        public IActionResult Index()
        {
            ViewBag.Nav = this.Nav("Manuales");
            return View();
        }

        [AccesoPublico]
        public IActionResult Participante() => Pdf(ManualesCatrinas.Participante);

        [SoloJurado]
        public IActionResult Jurado() => Pdf(ManualesCatrinas.Jurado);

        [SoloAdmin]
        public IActionResult Administracion() => Pdf(ManualesCatrinas.Administracion);

        // ?ver=1 entrega el PDF inline para que el navegador lo abra en su visor (boton "Ver", pestaña nueva); sin el
        // parametro se descarga como archivo (boton "Descargar PDF").
        private IActionResult Pdf(ManualUsuario manual)
        {
            var pdf = ManualUsuarioPdf.Generar(manual, DateTime.Today);
            if (Request.Query["ver"] != "1") return File(pdf, "application/pdf", manual.NombreArchivo);

            Response.Headers.ContentDisposition = new System.Net.Mime.ContentDisposition { Inline = true, FileName = manual.NombreArchivo }.ToString();
            return File(pdf, "application/pdf");
        }
    }
}
