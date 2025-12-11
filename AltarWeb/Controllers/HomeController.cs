using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly AltarDbContext _context;
        public HomeController(AltarDbContext context) { _context = context; }

        public IActionResult Menu()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            return View();
        }

        public IActionResult Historial()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");

            // Incluye Juez para mostrar nombre (JOIN)
            var lista = _context.Evaluaciones.Include(e => e.Juez).OrderByDescending(e => e.Fecha).ToList();
            return View(lista);
        }
    }
}