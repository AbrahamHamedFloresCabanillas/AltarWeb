using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Controllers
{
    public class JuecesController : Controller
    {
        private readonly AltarDbContext _context;

        public JuecesController(AltarDbContext context)
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

        // GET: Lista de Jueces (Solo Admins)
        public IActionResult Index()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para acceder a esta sección.";
                return RedirectToAction("Menu", "Home");
            }

            // Jueces activos (el query filter excluye los soft-deleted)
            var jueces = _context.Jueces.ToList();

            // Jueces inactivos (ignoramos el query filter)
            var inactivos = _context.Jueces
                .IgnoreQueryFilters()
                .Where(j => j.IsDeleted)
                .OrderByDescending(j => j.FechaEliminado)
                .ToList();

            ViewBag.Inactivos = inactivos;
            return View(jueces);
        }

        // GET: Crear (Solo Admins)
        public IActionResult Crear()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para crear jueces.";
                return RedirectToAction("Menu", "Home");
            }
            return View();
        }

        // POST: Crear (Solo Admins)
        [HttpPost]
        public IActionResult Crear(Juez juez)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para crear jueces.";
                return RedirectToAction("Menu", "Home");
            }

            // Buscar incluyendo soft-deleted para detectar conflictos
            var existente = _context.Jueces
                .IgnoreQueryFilters()
                .FirstOrDefault(j => j.Usuario == juez.Usuario);

            if (existente != null)
            {
                if (existente.IsDeleted)
                {
                    ViewBag.Error = "Este usuario está desactivado. Por favor, ve a la sección de inactivos para reactivarlo.";
                }
                else
                {
                    ViewBag.Error = "Ese usuario ya existe.";
                }
                return View(juez);
            }

            // Validar que el rol sea válido
            if (juez.Rol != "Admin" && juez.Rol != "Juez")
            {
                juez.Rol = "Juez"; // Default seguro
            }

            if (ModelState.IsValid)
            {
                _context.Jueces.Add(juez);
                _context.SaveChanges();
                TempData["Mensaje"] = $"Usuario '{juez.Usuario}' creado exitosamente con rol {juez.Rol}.";
                return RedirectToAction("Index");
            }
            return View(juez);
        }

        // GET: Editar (Solo Admins)
        public IActionResult Editar(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para editar jueces.";
                return RedirectToAction("Menu", "Home");
            }

            var juez = _context.Jueces.Find(id);
            if (juez == null) return NotFound();

            return View(juez);
        }

        // POST: Editar (Solo Admins)
        [HttpPost]
        public IActionResult Editar(Juez juez)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para editar jueces.";
                return RedirectToAction("Menu", "Home");
            }

            if (ModelState.IsValid)
            {
                // Verificamos que no duplique nombre de otro juez
                if (_context.Jueces.Any(j => j.Usuario == juez.Usuario && j.Id != juez.Id))
                {
                    ViewBag.Error = "Ya existe otro juez con este usuario.";
                    return View(juez);
                }

                // Validar que el rol sea válido
                if (juez.Rol != "Admin" && juez.Rol != "Juez")
                {
                    juez.Rol = "Juez";
                }

                _context.Jueces.Update(juez);
                _context.SaveChanges();
                TempData["Mensaje"] = $"Usuario '{juez.Usuario}' actualizado exitosamente.";
                return RedirectToAction("Index");
            }
            return View(juez);
        }

        // GET: Eliminar (Solo Admins) — SOFT DELETE
        public IActionResult Eliminar(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para eliminar jueces.";
                return RedirectToAction("Menu", "Home");
            }

            int? juezActual = HttpContext.Session.GetInt32("JuezId");
            if (id == juezActual)
            {
                TempData["Error"] = "No puedes eliminar tu propia cuenta mientras estás logueado.";
                return RedirectToAction("Index");
            }

            var juez = _context.Jueces.Find(id);
            if (juez != null)
            {
                // SOFT DELETE: marcar como eliminado en lugar de borrar
                juez.IsDeleted = true;
                juez.FechaEliminado = DateTime.UtcNow;
                _context.Jueces.Update(juez);
                _context.SaveChanges();
                TempData["Mensaje"] = $"El juez '{juez.Usuario}' ha sido desactivado. Sus evaluaciones se conservan.";
            }
            return RedirectToAction("Index");
        }

        // POST: Reactivar juez soft-deleted (Solo Admins)
        [HttpPost]
        public IActionResult Reactivar(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para reactivar usuarios.";
                return RedirectToAction("Menu", "Home");
            }

            var juez = _context.Jueces
                .IgnoreQueryFilters()
                .FirstOrDefault(j => j.Id == id && j.IsDeleted);

            if (juez != null)
            {
                juez.IsDeleted = false;
                juez.FechaEliminado = null;
                _context.Jueces.Update(juez);
                _context.SaveChanges();
                TempData["Mensaje"] = $"El juez '{juez.Usuario}' ha sido reactivado exitosamente.";
            }
            return RedirectToAction("Index");
        }

        // POST: Promover a Admin (Solo Admins)
        [HttpPost]
        public IActionResult Promover(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Acceso");
            if (!IsAdmin())
            {
                TempData["Error"] = "No tienes permisos para promover usuarios.";
                return RedirectToAction("Menu", "Home");
            }

            var juez = _context.Jueces.Find(id);
            if (juez != null)
            {
                juez.Rol = "Admin";
                _context.Jueces.Update(juez);
                _context.SaveChanges();
                TempData["Mensaje"] = $"'{juez.Usuario}' ha sido promovido a Administrador.";
            }
            return RedirectToAction("Index");
        }
    }
}