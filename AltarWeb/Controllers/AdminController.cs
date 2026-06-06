using AltarWeb.Models;
using AltarWeb.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Controllers
{
    public class AdminController : Controller
    {
        private readonly AltarDbContext _context;

        public AdminController(AltarDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Alumnos()
        {
            if (!PuedeAdministrar()) return RedirigirSinPermisos();

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var alumnos = await _context.Alumnos
                .IgnoreQueryFilters()
                .OrderBy(a => a.NombreCompleto)
                .ToListAsync();

            var equipos = await _context.AlumnoEquipos
                .IgnoreQueryFilters()
                .Include(ae => ae.Equipo)
                .Where(ae => ae.Equipo.PeriodoAcademico == periodo)
                .ToListAsync();

            ViewBag.EquiposActuales = equipos.ToDictionary(ae => ae.AlumnoId, ae => ae.Equipo.NombreEquipo);
            return View(alumnos);
        }

        public async Task<IActionResult> BuscarAlumnos(string q)
        {
            if (!PuedeAdministrar() || string.IsNullOrWhiteSpace(q)) return Json(new List<object>());

            var texto = q.ToLowerInvariant();
            var resultados = await _context.Alumnos
                .IgnoreQueryFilters()
                .Where(a => a.Matricula.Contains(q) || a.NombreCompleto.ToLower().Contains(texto))
                .OrderBy(a => a.NombreCompleto)
                .Take(10)
                .Select(a => new { a.Id, a.NombreCompleto, a.Matricula, a.CorreoElectronico, a.ProveedorAuth, a.Activo })
                .ToListAsync();

            return Json(resultados);
        }

        [HttpPost]
        public async Task<IActionResult> DesactivarAlumno(int id)
        {
            if (!PuedeAdministrar()) return RedirigirSinPermisos();

            var alumno = await _context.Alumnos.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
            if (alumno != null)
            {
                alumno.Activo = false;
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"Alumno '{alumno.NombreCompleto}' desactivado.";
            }

            return RedirectToAction("Alumnos");
        }

        [HttpPost]
        public async Task<IActionResult> ReactivarAlumno(int id)
        {
            if (!PuedeAdministrar()) return RedirigirSinPermisos();

            var alumno = await _context.Alumnos.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
            if (alumno != null)
            {
                alumno.Activo = true;
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"Alumno '{alumno.NombreCompleto}' reactivado.";
            }

            return RedirectToAction("Alumnos");
        }

        public async Task<IActionResult> Equipos()
        {
            if (!PuedeAdministrar()) return RedirigirSinPermisos();

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var equipos = await _context.Equipos
                .Include(e => e.Integrantes)
                    .ThenInclude(ae => ae.Alumno)
                .Where(e => e.PeriodoAcademico == periodo)
                .OrderBy(e => e.NombreEquipo)
                .ToListAsync();

            return View(equipos);
        }

        public async Task<IActionResult> EquiposHistorico()
        {
            if (!PuedeAdministrar()) return RedirigirSinPermisos();

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var equipos = await _context.Equipos
                .IgnoreQueryFilters()
                .Include(e => e.Integrantes)
                    .ThenInclude(ae => ae.Alumno)
                .Where(e => e.PeriodoAcademico != periodo || !e.Activo)
                .OrderByDescending(e => e.PeriodoAcademico)
                .ThenBy(e => e.NombreEquipo)
                .ToListAsync();

            return View("Equipos", equipos);
        }

        [HttpPost]
        public async Task<IActionResult> DesactivarEquipo(int id)
        {
            if (!PuedeAdministrar()) return RedirigirSinPermisos();

            var equipo = await _context.Equipos.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
            if (equipo != null)
            {
                equipo.Activo = false;
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"Equipo '{equipo.NombreEquipo}' desactivado.";
            }

            return RedirectToAction("Equipos");
        }

        [HttpPost]
        public async Task<IActionResult> ReactivarEquipo(int id)
        {
            if (!PuedeAdministrar()) return RedirigirSinPermisos();

            var equipo = await _context.Equipos.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
            if (equipo != null)
            {
                equipo.Activo = true;
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"Equipo '{equipo.NombreEquipo}' reactivado.";
            }

            return RedirectToAction("EquiposHistorico");
        }

        public async Task<IActionResult> EditarEquipo(int id)
        {
            if (!PuedeAdministrar()) return RedirigirSinPermisos();

            var equipo = await _context.Equipos
                .IgnoreQueryFilters()
                .Include(e => e.Integrantes)
                    .ThenInclude(ae => ae.Alumno)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (equipo == null) return NotFound();

            return View(new EditarEquipoViewModel
            {
                Id = equipo.Id,
                NombreEquipo = equipo.NombreEquipo,
                PeriodoAcademico = equipo.PeriodoAcademico,
                IntegranteIds = equipo.Integrantes.Select(i => i.AlumnoId).ToList()
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditarEquipo(int id, EditarEquipoViewModel model)
        {
            if (!PuedeAdministrar()) return RedirigirSinPermisos();
            if (id != model.Id) return BadRequest();

            var equipo = await _context.Equipos
                .IgnoreQueryFilters()
                .Include(e => e.Integrantes)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (equipo == null) return NotFound();

            if (!ModelState.IsValid) return View(model);

            var nombre = model.NombreEquipo.Trim();
            var duplicado = await _context.Equipos
                .IgnoreQueryFilters()
                .AnyAsync(e => e.Id != id && e.NombreEquipo == nombre && e.PeriodoAcademico == equipo.PeriodoAcademico);
            if (duplicado)
            {
                ModelState.AddModelError(nameof(model.NombreEquipo), "Ya existe un equipo con ese nombre en este periodo.");
                return View(model);
            }

            var nuevosIds = model.IntegranteIds.Distinct().ToList();
            if (nuevosIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "El equipo debe conservar al menos un integrante.");
                return View(model);
            }

            equipo.NombreEquipo = nombre;
            foreach (var actual in equipo.Integrantes.ToList())
            {
                if (!nuevosIds.Contains(actual.AlumnoId)) _context.AlumnoEquipos.Remove(actual);
            }

            foreach (var alumnoId in nuevosIds)
            {
                if (equipo.Integrantes.Any(i => i.AlumnoId == alumnoId)) continue;
                _context.AlumnoEquipos.Add(new AlumnoEquipo
                {
                    AlumnoId = alumnoId,
                    EquipoId = equipo.Id,
                    EsCreador = alumnoId == equipo.CreadoPorAlumnoId
                });
            }

            await _context.SaveChangesAsync();
            TempData["Mensaje"] = $"Equipo '{equipo.NombreEquipo}' actualizado.";
            return RedirectToAction("Equipos");
        }

        private bool PuedeAdministrar()
        {
            return HttpContext.Session.GetInt32("JuezId") != null
                && HttpContext.Session.GetString("JuezRol") == "Admin";
        }

        private IActionResult RedirigirSinPermisos()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            TempData["Error"] = "No tienes permisos para acceder a esta seccion.";
            return RedirectToAction("Menu", "Home");
        }
    }
}
