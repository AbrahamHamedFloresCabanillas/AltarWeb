using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc;

namespace AltarWeb.Controllers
{
    public class JuecesController : Controller
    {
        private readonly AltarDbContext _context;

        public JuecesController(AltarDbContext context)
        {
            _context = context;
        }

        // GET: Lista de Jueces
        public IActionResult Index()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");

            // Traemos todos los jueces
            var jueces = _context.Jueces.ToList();
            return View(jueces);
        }

        // GET: Crear
        public IActionResult Crear()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            return View();
        }

        // POST: Crear
        [HttpPost]
        public IActionResult Crear(Juez juez)
        {
            if (_context.Jueces.Any(j => j.Usuario == juez.Usuario))
            {
                ViewBag.Error = "Ese usuario ya existe.";
                return View(juez);
            }

            if (ModelState.IsValid)
            {
                _context.Jueces.Add(juez);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(juez);
        }

        // GET: Editar
        public IActionResult Editar(int id)
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");

            var juez = _context.Jueces.Find(id);
            if (juez == null) return NotFound();

            return View(juez);
        }

        // POST: Editar
        [HttpPost]
        public IActionResult Editar(Juez juez)
        {
            if (ModelState.IsValid)
            {
                // Verificamos que no duplique nombre de otro juez
                if (_context.Jueces.Any(j => j.Usuario == juez.Usuario && j.Id != juez.Id))
                {
                    ViewBag.Error = "Ya existe otro juez con este usuario.";
                    return View(juez);
                }

                _context.Jueces.Update(juez);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(juez);
        }

        // GET: Eliminar
        public IActionResult Eliminar(int id)
        {
            int? juezActual = HttpContext.Session.GetInt32("JuezId");
            if (juezActual == null) return RedirectToAction("Login", "Acceso");

            if (id == juezActual)
            {
                TempData["Error"] = "No puedes eliminar tu propia cuenta mientras estás logueado.";
                return RedirectToAction("Index");
            }

            var juez = _context.Jueces.Find(id);
            if (juez != null)
            {
                // Validar si tiene evaluaciones (Opcional: podrías impedir borrar si tiene evaluaciones)
                _context.Jueces.Remove(juez);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}