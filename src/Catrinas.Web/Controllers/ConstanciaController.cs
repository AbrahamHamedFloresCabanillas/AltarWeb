using Catrinas.Web.Datos;
using Catrinas.Web.Modelos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Web.Controllers
{
    // Constancias de Catrinas: solo para evaluaciones Final y siempre desde el snapshot de la evaluacion.
    public class ConstanciaController : Controller
    {
        private readonly CatrinasDbContext _context;
        private readonly ConstanciaCatrinaService _constancias;
        private readonly ILogger<ConstanciaController> _logger;

        public ConstanciaController(CatrinasDbContext context, ConstanciaCatrinaService constancias, ILogger<ConstanciaController> logger)
        {
            _context = context;
            _constancias = constancias;
            _logger = logger;
        }

        [SoloJurado]
        public async Task<IActionResult> Grupal(int evaluacionId) =>
            await ConFinalAsync(evaluacionId, e => File(_constancias.GenerarGrupal(e), "application/pdf",
                ConstanciaCatrinaService.NombreArchivo("Constancia_Grupal", e.SnapshotNombreInscripcion)));

        [SoloJurado]
        public async Task<IActionResult> Individuales(int evaluacionId) =>
            await ConFinalAsync(evaluacionId, e => File(_constancias.GenerarZipIndividuales(e), "application/zip",
                ComponentesConstancia.NombreArchivoSeguro($"Constancias_{e.SnapshotNombreInscripcion}.zip")));

        [SoloJurado]
        public async Task<IActionResult> Maestro(int evaluacionId) =>
            await ConFinalAsync(evaluacionId, e =>
            {
                if (string.IsNullOrWhiteSpace(e.SnapshotMaestroEncargado))
                {
                    TempData["Error"] = "Esta inscripción no tiene maestro encargado.";
                    return RedirectToAction("Detalle", "Evaluacion", new { id = e.Id });
                }
                return File(_constancias.GenerarMaestro(e, e.SnapshotMaestroEncargado), "application/pdf",
                    ConstanciaCatrinaService.NombreArchivo("Constancia_Maestro", e.SnapshotMaestroEncargado));
            });

        [SoloJurado]
        public async Task<IActionResult> Jurado(int evaluacionId) =>
            await ConFinalAsync(evaluacionId, e =>
            {
                var nombre = e.Juez?.NombreCompleto ?? e.Juez?.Usuario;
                if (string.IsNullOrWhiteSpace(nombre))
                {
                    TempData["Error"] = "Esta evaluación no tiene un integrante del jurado asignado.";
                    return RedirectToAction("Detalle", "Evaluacion", new { id = e.Id });
                }
                return File(_constancias.GenerarJuez(e, nombre), "application/pdf", ConstanciaCatrinaService.NombreArchivo("Constancia_Jurado", nombre));
            });

        [SoloJurado]
        [HttpPost]
        public async Task<IActionResult> EnviarTodas(int evaluacionId)
        {
            var evaluacion = await CargarAsync(evaluacionId);
            if (evaluacion == null) return NotFound();
            if (evaluacion.Estado != EstadoEvaluacion.Final)
            {
                TempData["Error"] = "Las constancias solo se generan para evaluaciones Final.";
                return RedirectToAction("Detalle", "Evaluacion", new { id = evaluacionId });
            }

            var inscripcion = await _context.Inscripciones.AsNoTracking()
                .Include(i => i.Responsable).Include(i => i.MaestroEncargado)
                .FirstAsync(i => i.Id == evaluacion.InscripcionId);
            try
            {
                await _constancias.EnviarAsync(evaluacion, inscripcion.Responsable.CorreoInstitucional,
                    evaluacion.SnapshotMaestroEncargado, inscripcion.MaestroEncargado?.CorreoInstitucional,
                    evaluacion.Juez?.NombreCompleto, evaluacion.Juez?.CorreoInstitucional);
                TempData["Mensaje"] = "Constancias enviadas por correo.";
            }
            catch (Exception ex)
            {
                // Sin el mensaje de la excepcion: SmtpFailedRecipientException incluye la direccion del destinatario.
                _logger.LogError("Error al enviar constancias de la evaluación {EvaluacionId}: {Tipo} {Estado}",
                    evaluacionId, ex.GetType().Name, (ex as System.Net.Mail.SmtpException)?.StatusCode);
                TempData["Error"] = "No se pudieron enviar todas las constancias por correo (algunas pudieron enviarse). Puedes descargarlas desde esta página.";
            }
            return RedirectToAction("Detalle", "Evaluacion", new { id = evaluacionId });
        }

        // Constancia propia del participante: solo la de su inscripcion y solo si es Final.
        [SoloParticipante]
        public async Task<IActionResult> Mia()
        {
            var registranteId = HttpContext.Session.Registrante()!.Value;
            var evaluacion = await _context.Evaluaciones
                .Include(e => e.Integrantes)
                .Where(e => e.Estado == EstadoEvaluacion.Final && e.Integrantes.Any(i => i.RegistranteId == registranteId))
                .OrderByDescending(e => e.FinalizadaEn)
                .FirstOrDefaultAsync();
            if (evaluacion == null)
            {
                TempData["Error"] = "Tu constancia estará disponible cuando el jurado cierre la evaluación de tu inscripción.";
                return RedirectToAction("Panel", "Registro");
            }

            var propio = evaluacion.Integrantes.First(i => i.RegistranteId == registranteId);
            return File(_constancias.GenerarIndividual(evaluacion, propio), "application/pdf",
                ConstanciaCatrinaService.NombreArchivo("Constancia", propio.NombreCompleto));
        }

        private async Task<IActionResult> ConFinalAsync(int evaluacionId, Func<EvaluacionCatrina, IActionResult> generar)
        {
            var evaluacion = await CargarAsync(evaluacionId);
            if (evaluacion == null) return NotFound();
            if (evaluacion.Estado != EstadoEvaluacion.Final)
            {
                TempData["Error"] = "Las constancias solo se generan para evaluaciones Final.";
                return RedirectToAction("Detalle", "Evaluacion", new { id = evaluacionId });
            }
            return generar(evaluacion);
        }

        private Task<EvaluacionCatrina?> CargarAsync(int evaluacionId) =>
            _context.Evaluaciones.Include(e => e.Juez).Include(e => e.Integrantes).FirstOrDefaultAsync(e => e.Id == evaluacionId);
    }
}
