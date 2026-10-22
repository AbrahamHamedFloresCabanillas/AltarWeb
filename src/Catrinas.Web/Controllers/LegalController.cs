using Microsoft.AspNetCore.Mvc;

namespace Catrinas.Web.Controllers
{
    // Aviso de privacidad integral y terminos de uso (2026-10-22). Publicos: se enlazan desde el inicio de sesion y
    // desde el registro, antes de que la persona tenga cuenta.
    [Route("Legal/{action}")]
    public class LegalController : Controller
    {
        private readonly IConfiguration _configuracion;
        public LegalController(IConfiguration configuracion) => _configuracion = configuracion;

        [AccesoPublico]
        public IActionResult Privacidad()
        {
            CargarDatos();
            return View();
        }

        [AccesoPublico]
        public IActionResult Terminos()
        {
            CargarDatos();
            return View();
        }

        private void CargarDatos()
        {
            ViewBag.Version = AvisosLegales.Version;
            ViewBag.Correo = AvisosLegales.Dato(_configuracion, "CorreoPrivacidad");
            ViewBag.Domicilio = AvisosLegales.Dato(_configuracion, "Domicilio");
            ViewBag.Plazo = AvisosLegales.Dato(_configuracion, "PlazoConservacion");
        }
    }
}
