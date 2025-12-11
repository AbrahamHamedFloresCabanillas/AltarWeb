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
                return RedirectToAction("Menu", "Home");
            }
            ViewBag.Error = "Usuario o contraseña incorrectos";
            return View();
        }

        public IActionResult Registrar() { return View(); }

        [HttpPost]
        public IActionResult Registrar(string usuario, string password, string confirmar)
        {
            if (password != confirmar) { ViewBag.Error = "Las contraseñas no coinciden"; return View(); }
            if (_context.Jueces.Any(j => j.Usuario == usuario)) { ViewBag.Error = "Usuario ya existe"; return View(); }

            _context.Jueces.Add(new Juez { Usuario = usuario, Password = password });
            _context.SaveChanges();
            return RedirectToAction("Login");
        }

        public IActionResult Salir()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}