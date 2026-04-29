using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc;

namespace AltarWeb.Controllers
{
    public class AccesoController : Controller
    {
        private readonly AltarDbContext _context;

        public AccesoController(AltarDbContext context) { _context = context; }

        public IActionResult Login() { return View(); }

        [HttpPost]
        public IActionResult Login(string usuario, string password)
        {
            var juez = _context.Jueces.FirstOrDefault(j => j.Usuario == usuario && j.Password == password);
            if (juez != null)
            {
                HttpContext.Session.SetInt32("JuezId", juez.Id); // Guardar sesión
                HttpContext.Session.SetString("JuezNombre", juez.Usuario);
                HttpContext.Session.SetString("JuezRol", juez.Rol); // RBAC: guardar rol en sesión
                return RedirectToAction("Menu", "Home");
            }
            ViewBag.Error = "Usuario o contraseña incorrectos";
            return View();
        }

        // Registro público deshabilitado — solo Admins pueden crear Jueces desde /Jueces/Crear
        public IActionResult Registrar()
        {
            return RedirectToAction("Login");
        }

        public IActionResult Salir()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}