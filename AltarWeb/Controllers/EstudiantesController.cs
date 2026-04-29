using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Controllers
{
    public class EstudiantesController : Controller
    {
        private readonly AltarDbContext _context;

        public EstudiantesController(AltarDbContext context)
        {
            _context = context;
        }

        // Helper: verificar si el usuario actual es Admin
        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("JuezRol") == "Admin";
        }

        // Helper: verificar si hay sesión activa
        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetInt32("JuezId") != null;
        }

        // GET: Lista de Estudiantes/Integrantes (Solo Admins)
        public IActionResult Index()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para acceder a esta sección.";
                return RedirectToAction("Menu", "Home");
            }

            // Estudiantes activos (el query filter excluye los soft-deleted)
            var estudiantes = _context.Integrantes
                .Include(i => i.Evaluacion)
                .OrderBy(i => i.Nombre)
                .ToList();

            // Estudiantes inactivos (ignoramos el query filter)
            var inactivos = _context.Integrantes
                .IgnoreQueryFilters()
                .Include(i => i.Evaluacion)
                .Where(i => i.IsDeleted)
                .OrderByDescending(i => i.FechaEliminado)
                .ToList();

            ViewBag.Inactivos = inactivos;
            return View(estudiantes);
        }

        // GET: Eliminar (Solo Admins) — SOFT DELETE
        public IActionResult Eliminar(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para eliminar estudiantes.";
                return RedirectToAction("Menu", "Home");
            }

            var integrante = _context.Integrantes.Find(id);
            if (integrante != null)
            {
                // SOFT DELETE: marcar como eliminado en lugar de borrar
                integrante.IsDeleted = true;
                integrante.FechaEliminado = DateTime.UtcNow;
                _context.Integrantes.Update(integrante);
                _context.SaveChanges();
                TempData["Mensaje"] = $"El estudiante '{integrante.Nombre}' ha sido desactivado. Su historial se conserva.";
            }
            return RedirectToAction("Index");
        }

        // POST: Reactivar estudiante soft-deleted (Solo Admins)
        [HttpPost]
        public IActionResult Reactivar(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para reactivar estudiantes.";
                return RedirectToAction("Menu", "Home");
            }

            var integrante = _context.Integrantes
                .IgnoreQueryFilters()
                .FirstOrDefault(i => i.Id == id && i.IsDeleted);

            if (integrante != null)
            {
                integrante.IsDeleted = false;
                integrante.FechaEliminado = null;
                _context.Integrantes.Update(integrante);
                _context.SaveChanges();
                TempData["Mensaje"] = $"El estudiante '{integrante.Nombre}' ha sido reactivado exitosamente.";
            }
            return RedirectToAction("Index");
        }
    }
}
