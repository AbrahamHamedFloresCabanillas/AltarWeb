using AltarWeb.Models;
using AltarWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace AltarWeb.Controllers
{
    public class ConstanciaController : Controller
    {
        private readonly AltarDbContext _context;
        private readonly ConstanciaService _constancias;

        public ConstanciaController(AltarDbContext context, ConstanciaService constancias)
        {
            _context = context;
            _constancias = constancias;
        }

        public async Task<IActionResult> DescargarGrupal(int evaluacionId)
        {
            if (!EstaAutenticado()) return RedirectToAction("Login", "Acceso");
            var eval = await ObtenerEvaluacionAsync(evaluacionId);
            if (eval == null) return NotFound();
            if (eval.Estado != Models.Registro.EstadoEvaluacion.Final) return Forbid();

            var pdf = _constancias.GenerarGrupal(eval);
            return File(pdf, "application/pdf", $"Constancia_Grupal_{eval.SnapshotNombreEquipo}.pdf");
        }

        public async Task<IActionResult> DescargarIndividuales(int evaluacionId)
        {
            if (!EstaAutenticado()) return RedirectToAction("Login", "Acceso");
            var eval = await ObtenerEvaluacionAsync(evaluacionId);
            if (eval == null) return NotFound();
            if (eval.Estado != Models.Registro.EstadoEvaluacion.Final) return Forbid();

            var zip = _constancias.GenerarZipIndividuales(eval);
            return File(zip, "application/zip", $"Constancias_Individuales_{eval.SnapshotNombreEquipo}.zip");
        }

        public async Task<IActionResult> DescargarMaestro(int evaluacionId)
        {
            if (!EstaAutenticado()) return RedirectToAction("Login", "Acceso");
            var eval = await ObtenerEvaluacionAsync(evaluacionId);
            if (eval == null) return NotFound();
            if (eval.Estado != Models.Registro.EstadoEvaluacion.Final) return Forbid();

            var nombreMaestro = eval.Equipo.MaestroEncargado?.NombreCompleto;
            if (string.IsNullOrWhiteSpace(nombreMaestro))
            {
                TempData["Error"] = "Este equipo no tiene un maestro encargado registrado.";
                return RedirectToAction("Detalle", "AltarEvaluacion", new { id = evaluacionId });
            }

            var pdf = _constancias.GenerarMaestroEncargado(eval, nombreMaestro);
            return File(pdf, "application/pdf", $"Constancia_Maestro_{nombreMaestro}.pdf");
        }

        public async Task<IActionResult> DescargarJuez(int evaluacionId)
        {
            if (!EstaAutenticado()) return RedirectToAction("Login", "Acceso");
            var eval = await ObtenerEvaluacionAsync(evaluacionId);
            if (eval == null) return NotFound();
            if (eval.Estado != Models.Registro.EstadoEvaluacion.Final) return Forbid();

            var nombreJuez = eval.Juez?.NombreCompleto ?? eval.Juez?.Usuario;
            if (string.IsNullOrWhiteSpace(nombreJuez))
            {
                TempData["Error"] = "Esta evaluación no tiene un juez asignado.";
                return RedirectToAction("Detalle", "AltarEvaluacion", new { id = evaluacionId });
            }

            var pdf = _constancias.GenerarJuez(eval, nombreJuez);
            return File(pdf, "application/pdf", $"Constancia_Juez_{nombreJuez}.pdf");
        }

        [HttpPost]
        public async Task<IActionResult> EnviarTodas(int evaluacionId)
        {
            if (!EstaAutenticado()) return RedirectToAction("Login", "Acceso");
            var eval = await ObtenerEvaluacionAsync(evaluacionId);
            if (eval == null) return NotFound();
            if (eval.Estado != Models.Registro.EstadoEvaluacion.Final) return Forbid();

            var nombreMaestro = eval.Equipo.MaestroEncargado?.NombreCompleto ?? string.Empty;
            var correoMaestro = eval.Equipo.MaestroEncargado?.CorreoInstitucional;
            var nombreJuez = eval.Juez?.NombreCompleto ?? eval.Juez?.Usuario ?? string.Empty;

            try
            {
                await _constancias.EnviarConstancias(
                    eval,
                    correoCreador: eval.Equipo.CreadoPorRegistrante?.CorreoInstitucional,
                    nombreMaestro: nombreMaestro,
                    correoMaestro: correoMaestro,
                    nombreJuez: nombreJuez,
                    correoJuez: null);

                TempData["Mensaje"] = "Constancias enviadas por correo.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Detalle", "AltarEvaluacion", new { id = evaluacionId });
        }

        public async Task<IActionResult> DescargarMia()
        {
            var registranteId = HttpContext.Session.GetInt32("RegistranteId");
            if (registranteId == null) return RedirectToAction("Login", "Registro");

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var eval = await _context.EvaluacionesConcurso
                .Include(e => e.Equipo)
                .Include(e => e.Integrantes)
                .Where(e => e.Periodo == periodo && e.Estado == Models.Registro.EstadoEvaluacion.Final)
                .Where(e => e.Equipo.Integrantes.Any(ri => ri.RegistranteId == registranteId.Value))
                .FirstOrDefaultAsync();

            if (eval == null)
            {
                TempData["Error"] = "Tu constancia estará disponible cuando tu equipo tenga una evaluación Final.";
                return RedirectToAction("Dashboard", "Registro");
            }

            var integrante = eval.Integrantes.FirstOrDefault(i => i.RegistranteId == registranteId.Value);
            var registrante = await _context.Registrantes.FindAsync(registranteId.Value);
            var nombre = integrante?.NombreCompleto ?? registrante?.NombreCompleto ?? "Participante";

            var pdf = _constancias.GenerarIndividual(eval, nombre);
            return File(pdf, "application/pdf", $"Constancia_{nombre}.pdf");
        }

        private async Task<Evaluacion?> ObtenerEvaluacionAsync(int evaluacionId)
        {
            return await _context.EvaluacionesConcurso
                .Include(e => e.Equipo).ThenInclude(eq => eq.Carrera)
                .Include(e => e.Equipo).ThenInclude(eq => eq.MaestroEncargado)
                .Include(e => e.Equipo).ThenInclude(eq => eq.CreadoPorRegistrante)
                .Include(e => e.Juez)
                .Include(e => e.Integrantes).ThenInclude(i => i.Registrante)
                .FirstOrDefaultAsync(e => e.Id == evaluacionId);
        }

        private bool EstaAutenticado() => HttpContext.Session.GetInt32("JuezId") != null;
    }
}
