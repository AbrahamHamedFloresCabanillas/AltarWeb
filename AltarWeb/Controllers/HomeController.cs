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

            // Pasar el rol a la vista para mostrar/ocultar opciones de admin
            ViewBag.EsAdmin = HttpContext.Session.GetString("JuezRol") == "Admin";
            ViewBag.JuezNombre = HttpContext.Session.GetString("JuezNombre") ?? "Usuario";
            return View();
        }

        public IActionResult Historial()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");

            // Incluye Juez para mostrar nombre (JOIN) — IgnoreQueryFilters para ver jueces soft-deleted
            var lista = _context.Evaluaciones
                .IgnoreQueryFilters()
                .Include(e => e.Juez)
                .OrderByDescending(e => e.Fecha)
                .ToList();

            // Agrupar por Periodo para la vista
            var agrupado = lista
                .GroupBy(e => string.IsNullOrEmpty(e.Periodo) ? "Sin Periodo" : e.Periodo)
                .OrderByDescending(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            ViewBag.EsAdmin = HttpContext.Session.GetString("JuezRol") == "Admin";
            return View(agrupado);
        }
    }
}