using AltarWeb.Models;
using AltarWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Controllers
{
    public class EvaluacionController : Controller
    {
        private readonly AltarDbContext _context;
        private readonly ConstanciaService _constanciaService;

        public EvaluacionController(AltarDbContext context, ConstanciaService constanciaService)
        {
            _context = context;
            _constanciaService = constanciaService;
        }

        public IActionResult Crear()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            ViewBag.EsAdmin = HttpContext.Session.GetString("JuezRol") == "Admin";
            return View(new Evaluacion());
        }

        [HttpPost]
        public async Task<IActionResult> Crear(Evaluacion eval, int? EquipoId)
        {
            int? juezId = HttpContext.Session.GetInt32("JuezId");
            if (juezId == null) return RedirectToAction("Login", "Acceso");

            ModelState.Remove("Juez");
            ModelState.Remove("Equipo");
            ModelState.Remove("Integrantes");
            ModelState.Remove("Periodo");
            ModelState.Remove("NombreJuez");
            ModelState.Remove("NombreEquipo");
            ModelState.Remove("SnapshotNombreEquipo");

            if (EquipoId == null)
            {
                TempData["Error"] = "Selecciona el equipo a evaluar.";
                ViewBag.EsAdmin = HttpContext.Session.GetString("JuezRol") == "Admin";
                return View(eval);
            }

            var periodoActual = PeriodoHelper.ObtenerPeriodoActual();
            var incluirHistorico = HttpContext.Session.GetString("JuezRol") == "Admin";
            var equipo = await _context.Equipos
                .IgnoreQueryFilters()
                .Include(e => e.Integrantes)
                    .ThenInclude(ae => ae.Alumno)
                .FirstOrDefaultAsync(e => e.Id == EquipoId.Value && e.Activo && (incluirHistorico || e.PeriodoAcademico == periodoActual));

            if (equipo == null)
            {
                TempData["Error"] = "El equipo seleccionado no esta disponible.";
                ViewBag.EsAdmin = incluirHistorico;
                return View(eval);
            }

            var yaEvaluado = await _context.Evaluaciones.AnyAsync(e => e.EquipoId == equipo.Id);
            if (yaEvaluado)
            {
                TempData["Error"] = $"El equipo '{equipo.NombreEquipo}' ya tiene una evaluacion registrada.";
                ViewBag.EsAdmin = incluirHistorico;
                return View(eval);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.EsAdmin = incluirHistorico;
                return View(eval);
            }

            var juez = await _context.Jueces.FindAsync(juezId.Value);
            eval.JuezId = juezId.Value;
            eval.NombreJuez = !string.IsNullOrWhiteSpace(juez?.NombreCompleto) ? juez.NombreCompleto : juez?.Usuario ?? "Desconocido";
            eval.EquipoId = equipo.Id;
            eval.NombreEquipo = equipo.NombreEquipo;
            eval.SnapshotNombreEquipo = equipo.NombreEquipo;
            eval.Periodo = equipo.PeriodoAcademico;
            eval.Fecha = DateTime.Now;
            eval.CalcularNotasFinales();

            _context.Evaluaciones.Add(eval);
            await _context.SaveChangesAsync();

            if (eval.NotaFinal >= 9.0m)
            {
                try
                {
                    await _constanciaService.EnviarConstancias(eval, equipo.Integrantes.ToList());
                }
                catch (Exception ex)
                {
                    TempData["Mensaje"] = "Evaluacion guardada. No se pudieron enviar las constancias: " + ex.Message;
                    return RedirectToAction("Detalle", new { id = eval.Id });
                }
            }

            TempData["Mensaje"] = "Evaluacion guardada exitosamente.";
            return RedirectToAction("Detalle", new { id = eval.Id });
        }

        public async Task<IActionResult> Detalle(int id)
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            var eval = await _context.Evaluaciones
                .IgnoreQueryFilters()
                .Include(e => e.Integrantes)
                .Include(e => e.Juez)
                .Include(e => e.Equipo)
                    .ThenInclude(eq => eq!.Integrantes)
                        .ThenInclude(ae => ae.Alumno)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (eval == null) return NotFound();
            if (TempData["Mensaje"] != null) ViewBag.Mensaje = TempData["Mensaje"];
            return View(eval);
        }

        public async Task<IActionResult> BuscarEquipos(string q, bool incluirHistorico = false)
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return Json(new List<object>());
            var esAdmin = HttpContext.Session.GetString("JuezRol") == "Admin";
            var periodo = PeriodoHelper.ObtenerPeriodoActual();

            var query = _context.Equipos
                .IgnoreQueryFilters()
                .Include(e => e.Integrantes)
                .Where(e => e.Activo)
                .AsQueryable();

            if (!incluirHistorico || !esAdmin)
            {
                query = query.Where(e => e.PeriodoAcademico == periodo);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var texto = q.ToLower();
                query = query.Where(e => e.NombreEquipo.ToLower().Contains(texto));
            }

            var resultados = await query
                .OrderBy(e => e.NombreEquipo)
                .Take(10)
                .Select(e => new
                {
                    e.Id,
                    e.NombreEquipo,
                    e.PeriodoAcademico,
                    CantidadIntegrantes = e.Integrantes.Count
                })
                .ToListAsync();

            return Json(resultados);
        }

        public async Task<IActionResult> ObtenerIntegrantesEquipo(int equipoId)
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return Json(new List<object>());

            var integrantes = await _context.AlumnoEquipos
                .IgnoreQueryFilters()
                .Include(ae => ae.Alumno)
                .Where(ae => ae.EquipoId == equipoId)
                .OrderByDescending(ae => ae.EsCreador)
                .ThenBy(ae => ae.Alumno.NombreCompleto)
                .Select(ae => new
                {
                    ae.Alumno.Id,
                    ae.Alumno.NombreCompleto,
                    ae.Alumno.Matricula,
                    ae.Alumno.CorreoElectronico,
                    ae.EsCreador
                })
                .ToListAsync();

            return Json(integrantes);
        }

        public async Task<IActionResult> EnviarResultados(int id)
        {
            return await EnviarConstancias(id);
        }

        [HttpPost]
        public async Task<IActionResult> EnviarConstanciasPost(int id)
        {
            return await EnviarConstancias(id);
        }

        [HttpPost]
        [ActionName("EnviarConstancias")]
        public async Task<IActionResult> EnviarConstanciasAction(int id)
        {
            return await EnviarConstancias(id);
        }

        private async Task<IActionResult> EnviarConstancias(int id)
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            var eval = await ObtenerEvaluacionConEquipoAsync(id);
            if (eval?.Equipo == null)
            {
                TempData["Mensaje"] = "Esta evaluacion no tiene equipo vinculado para enviar constancias nuevas.";
                return RedirectToAction("Historial", "Home");
            }

            try
            {
                await _constanciaService.EnviarConstancias(eval, eval.Equipo.Integrantes.ToList());
                TempData["Mensaje"] = "Constancias enviadas exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Mensaje"] = "No se pudieron enviar las constancias: " + ex.Message;
            }

            return RedirectToAction("Detalle", new { id });
        }

        public async Task<IActionResult> DescargarConstancia(int id)
        {
            return await DescargarGrupal(id);
        }

        public async Task<IActionResult> DescargarGrupal(int id)
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            var eval = await ObtenerEvaluacionConEquipoAsync(id);
            if (eval == null) return NotFound();

            var pdf = _constanciaService.GenerarGrupal(eval, eval.ObtenerNombreEquipo());
            return File(pdf, "application/pdf", $"Constancia_Grupal_{eval.ObtenerNombreEquipo()}.pdf");
        }

        public async Task<IActionResult> DescargarIndividuales(int id)
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            var eval = await ObtenerEvaluacionConEquipoAsync(id);
            if (eval?.Equipo == null) return NotFound();

            var zip = _constanciaService.GenerarZipIndividuales(eval, eval.Equipo.Integrantes.ToList());
            return File(zip, "application/zip", $"Constancias_Individuales_{eval.ObtenerNombreEquipo()}.zip");
        }

        private async Task<Evaluacion?> ObtenerEvaluacionConEquipoAsync(int id)
        {
            return await _context.Evaluaciones
                .IgnoreQueryFilters()
                .Include(e => e.Equipo)
                    .ThenInclude(eq => eq!.Integrantes)
                        .ThenInclude(ae => ae.Alumno)
                .FirstOrDefaultAsync(e => e.Id == id);
        }
    }
}
