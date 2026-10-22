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

        private FileContentResult Pdf(ManualUsuario manual) =>
            File(ManualUsuarioPdf.Generar(manual, DateTime.Today), "application/pdf", manual.NombreArchivo);
    }
}
