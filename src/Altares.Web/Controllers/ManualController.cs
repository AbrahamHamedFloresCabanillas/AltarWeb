using AltarWeb.Services;
using Concurso.Core.Pdf;
using Microsoft.AspNetCore.Mvc;

namespace AltarWeb.Controllers
{
    // Manuales de usuario en PDF, uno por rol (peticion del propietario, 2026-10-22). La pagina "Manuales" del menu
    // lateral es para jueces y administradores; el manual del participante es publico (no tiene datos de nadie) para
    // enlazarlo desde su portal; el del juez pide sesion de juez y el de administrador, rol Admin.
    // Ruta propia: la convencional de la app tiene action=Login por defecto, y aqui "/Manual" es la pagina Index.
    [Route("Manual/{action=Index}")]
    public class ManualController : Controller
    {
        public IActionResult Index()
        {
            if (!EsJuez()) return RedirectToAction("Login", "Registro");
            ViewBag.Nav = new AltarWeb.ViewModels.Altar.AltarNavContext
            {
                ActiveItem = "Manuales",
                NombreCompleto = HttpContext.Session.GetString("JuezNombre") ?? "Usuario",
                Rol = HttpContext.Session.GetString("JuezRol") ?? "Juez",
                EsAdmin = EsAdmin()
            };
            ViewBag.EsAdmin = EsAdmin();
            return View();
        }

        public IActionResult Participante() => Pdf(ManualesAltares.Participante);

        public IActionResult Juez() => EsJuez() ? Pdf(ManualesAltares.Juez) : RedirectToAction("Login", "Registro");

        public IActionResult Administrador()
        {
            if (!EsJuez()) return RedirectToAction("Login", "Registro");
            if (!EsAdmin())
            {
                TempData["Error"] = "No tienes permisos para acceder a esta sección.";
                return RedirectToAction("Index");
            }
            return Pdf(ManualesAltares.Administrador);
        }

        private FileContentResult Pdf(ManualUsuario manual) =>
            File(ManualUsuarioPdf.Generar(manual, DateTime.Today), "application/pdf", manual.NombreArchivo);

        private bool EsJuez() => HttpContext.Session.GetInt32("JuezId") != null;

        private bool EsAdmin() => EsJuez() && HttpContext.Session.GetString("JuezRol") == "Admin";
    }
}
