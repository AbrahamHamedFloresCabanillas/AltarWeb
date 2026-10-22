using Catrinas.Web.Datos;
using Catrinas.Web.Modelos;
using Catrinas.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Web.Controllers
{
    // Flujo del jurado de Catrinas: listado por carrera, evaluacion Preliminar (editable), cierre Final
    // con confirmacion explicita, detalle y ranking por carrera.
    [SoloJurado]
    public class EvaluacionController : Controller
    {
        public const string ConfirmacionFinal = "CONFIRMO";
        private readonly CatrinasDbContext _context;
        private readonly ILogger<EvaluacionController> _logger;

        public EvaluacionController(CatrinasDbContext context, ILogger<EvaluacionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? carreraId)
        {
            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var consulta = _context.Inscripciones
                .Include(i => i.Carrera)
                .Include(i => i.Evaluacion)
                .Include(i => i.Miembros).ThenInclude(m => m.Registrante)
                .AsSplitQuery()
                .Where(i => i.Activo && i.Periodo == periodo);
            if (carreraId is > 0) consulta = consulta.Where(i => i.CarreraId == carreraId);

            var filas = (await consulta.OrderBy(i => i.Carrera.Orden).ThenBy(i => i.Nombre).ToListAsync())
                .Select(i => new FilaInscripcionViewModel
                {
                    InscripcionId = i.Id,
                    Nombre = i.Nombre,
                    Carrera = i.Carrera.Nombre,
                    Personificador = i.Miembros.FirstOrDefault(m => m.Rol == RolInscripcion.Personificador)?.Registrante.NombreCompleto,
                    Integrantes = i.Miembros.Count,
                    Estado = i.Evaluacion?.Estado,
                    EvaluacionId = i.Evaluacion?.Id,
                    Nota = i.Evaluacion?.NotaCatrina,
                    Lugar = i.Evaluacion?.Lugar,
                    Faltantes = ReglasInscripcion.Faltantes(i),
                    AltarExternalId = i.AltarExternalId
                }).ToList();

            ViewBag.Nav = this.Nav("Inscripciones");
            ViewBag.Periodo = periodo;
            ViewBag.CarreraId = carreraId;
            ViewBag.Carreras = await _context.Carreras.Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync();
            return View(filas);
        }

        public async Task<IActionResult> Evaluar(int inscripcionId)
        {
            var inscripcion = await CargarInscripcionAsync(inscripcionId);
            if (inscripcion == null) return NotFound();

            var config = await ConfiguracionCatrinas.ObtenerAsync(_context, inscripcion.Periodo);
            var evaluacion = inscripcion.Evaluacion;
            ViewBag.Nav = this.Nav("Inscripciones");
            return View(new EvaluarViewModel
            {
                InscripcionId = inscripcion.Id,
                Nombre = inscripcion.Nombre,
                Carrera = inscripcion.Carrera.Nombre,
                Personificador = inscripcion.Miembros.FirstOrDefault(m => m.Rol == RolInscripcion.Personificador)?.Registrante.NombreCompleto,
                Integrantes = inscripcion.Miembros.Select(m => (m.Registrante.NombreCompleto, m.Rol)).ToList(),
                MaestroEncargado = inscripcion.MaestroEncargado?.NombreCompleto,
                AltarExternalId = inscripcion.AltarExternalId,
                AltarNombre = inscripcion.AltarNombreSnapshot,
                Faltantes = ReglasInscripcion.Faltantes(inscripcion),
                Estado = evaluacion?.Estado,
                EvaluacionId = evaluacion?.Id,
                RowVersion = evaluacion == null ? null : Convert.ToBase64String(evaluacion.RowVersion),
                Vestuario = evaluacion?.Vestuario ?? 0,
                Maquillaje = evaluacion?.Maquillaje ?? 0,
                Accesorios = evaluacion?.Accesorios ?? 0,
                OriginalidadCreatividad = evaluacion?.OriginalidadCreatividad ?? 0,
                ApegoTradiciones = evaluacion?.ApegoTradiciones ?? 0,
                // Una evaluacion ya guardada muestra los pesos con los que se calculo, no los actuales.
                Pesos = evaluacion?.Estado == EstadoEvaluacion.Final ? PesosDe(evaluacion) : config,
                PesosValidos = CalculoCatrina.PesosValidos(config)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Guardar(int inscripcionId, decimal vestuario, decimal maquillaje, decimal accesorios,
            decimal originalidadCreatividad, decimal apegoTradiciones, string accion, string? confirmacion, string? rowVersion)
        {
            var juezId = HttpContext.Session.JuezId()!.Value;
            var inscripcion = await CargarInscripcionAsync(inscripcionId);
            if (inscripcion == null) return NotFound();

            var cerrarFinal = accion == "final";
            var criterios = new[] { vestuario, maquillaje, accesorios, originalidadCreatividad, apegoTradiciones };
            // Un valor no numerico (formulario alterado) llega como 0 con ModelState invalido: tambien se rechaza
            // (prueba adversarial 2026-10-21; antes se guardaba como 0).
            var campos = new[] { nameof(vestuario), nameof(maquillaje), nameof(accesorios), nameof(originalidadCreatividad), nameof(apegoTradiciones) };
            if (criterios.Any(c => c < 0 || c > 10) || campos.Any(c => ModelState[c]?.ValidationState == ModelValidationState.Invalid))
            {
                return Volver(inscripcionId, "Cada criterio debe calificarse entre 0 y 10.");
            }

            if (inscripcion.Evaluacion?.Estado == EstadoEvaluacion.Final)
            {
                TempData["Error"] = "Esta evaluación ya fue cerrada como Final y no puede modificarse.";
                return RedirectToAction(nameof(Detalle), new { id = inscripcion.Evaluacion.Id });
            }
            if (cerrarFinal && confirmacion != ConfirmacionFinal)
            {
                return Volver(inscripcionId, "Para cerrar como Final confirma explícitamente que revisaste las calificaciones.");
            }
            if (cerrarFinal && !ReglasInscripcion.EstaListaParaFinal(inscripcion, out var motivo))
            {
                return Volver(inscripcionId, $"No se puede cerrar como Final: {motivo}.");
            }

            var config = await ConfiguracionCatrinas.ObtenerAsync(_context, inscripcion.Periodo);
            if (!CalculoCatrina.PesosValidos(config))
            {
                return Volver(inscripcionId, "Los pesos de los criterios configurados no suman 100%. Pide a la administración que los corrija.");
            }

            var evaluacion = inscripcion.Evaluacion;
            var esNueva = evaluacion == null;
            if (evaluacion == null)
            {
                evaluacion = new EvaluacionCatrina { InscripcionId = inscripcion.Id, Periodo = inscripcion.Periodo };
                _context.Evaluaciones.Add(evaluacion);
            }
            else
            {
                // Concurrencia optimista: si otro juez guardo despues de que este abrio la pantalla, se rechaza.
                // Un rowVersion ausente o mal formado (formulario alterado) se trata igual, sin excepcion (F4: antes era 500).
                var rowVersionEnviado = DecodificarRowVersion(rowVersion);
                if (rowVersionEnviado == null || !evaluacion.RowVersion.SequenceEqual(rowVersionEnviado))
                {
                    return Volver(inscripcionId, "Otro integrante del jurado modificó esta evaluación mientras la tenías abierta. Revisa los valores actuales.");
                }
                _context.Entry(evaluacion).Property(e => e.RowVersion).OriginalValue = rowVersionEnviado;
            }

            evaluacion.JuezId = juezId;
            evaluacion.Vestuario = vestuario;
            evaluacion.Maquillaje = maquillaje;
            evaluacion.Accesorios = accesorios;
            evaluacion.OriginalidadCreatividad = originalidadCreatividad;
            evaluacion.ApegoTradiciones = apegoTradiciones;
            // Los pesos se fijan al guardar (como en Altares): cambiarlos despues no recalcula lo ya guardado.
            evaluacion.PesoVestuario = config.PesoVestuario;
            evaluacion.PesoMaquillaje = config.PesoMaquillaje;
            evaluacion.PesoAccesorios = config.PesoAccesorios;
            evaluacion.PesoOriginalidadCreatividad = config.PesoOriginalidadCreatividad;
            evaluacion.PesoApegoTradiciones = config.PesoApegoTradiciones;
            evaluacion.NotaCatrina = CalculoCatrina.CalcularNota(vestuario, maquillaje, accesorios, originalidadCreatividad, apegoTradiciones, config);

            var personificador = inscripcion.Miembros.FirstOrDefault(m => m.Rol == RolInscripcion.Personificador)?.Registrante;
            evaluacion.SnapshotNombreInscripcion = inscripcion.Nombre;
            evaluacion.SnapshotCarrera = inscripcion.Carrera.Nombre;
            evaluacion.SnapshotPersonificador = personificador?.NombreCompleto ?? "(sin designar)";
            evaluacion.SnapshotMaestroEncargado = inscripcion.MaestroEncargado?.NombreCompleto;
            evaluacion.SnapshotAltarExternalId = inscripcion.AltarExternalId;
            evaluacion.SnapshotAltarNombre = inscripcion.AltarNombreSnapshot;
            evaluacion.ActualizadoEn = DateTime.UtcNow;

            _context.IntegrantesEvaluados.RemoveRange(evaluacion.Integrantes);
            evaluacion.Integrantes = inscripcion.Miembros.Select(m => new IntegranteEvaluado
            {
                RegistranteId = m.RegistranteId,
                NombreCompleto = m.Registrante.NombreCompleto,
                Identificador = m.Registrante.Identificador,
                CorreoInstitucional = m.Registrante.CorreoInstitucional,
                Rol = m.Rol
            }).ToList();

            if (cerrarFinal)
            {
                evaluacion.Estado = EstadoEvaluacion.Final;
                evaluacion.FinalizadaEn = DateTime.UtcNow;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Volver(inscripcionId, "Otro integrante del jurado modificó esta evaluación mientras la tenías abierta. Revisa los valores actuales.");
            }
            catch (Exception ex) when (esNueva && (ex is DbUpdateException || ex is InvalidOperationException))
            {
                // Dos jueces creando la evaluacion de la misma inscripcion a la vez: el indice unico gana.
                _logger.LogInformation("Evaluación duplicada evitada por el índice único. InscripcionId: {InscripcionId}", inscripcionId);
                return Volver(inscripcionId, "Otro integrante del jurado registró la evaluación de esta inscripción mientras guardabas la tuya.");
            }

            if (cerrarFinal)
            {
                await CalculoCatrina.RecalcularLugaresAsync(_context, inscripcion.CarreraId, inscripcion.Periodo);
                TempData["Mensaje"] = "Evaluación cerrada como Final.";
                return RedirectToAction(nameof(Detalle), new { id = evaluacion.Id });
            }

            TempData["Mensaje"] = "Evaluación guardada como Preliminar.";
            return RedirectToAction(nameof(Evaluar), new { inscripcionId });
        }

        public async Task<IActionResult> Detalle(int id)
        {
            var evaluacion = await _context.Evaluaciones
                .Include(e => e.Juez)
                .Include(e => e.Integrantes)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (evaluacion == null) return NotFound();

            ViewBag.Nav = this.Nav("Inscripciones");
            return View(evaluacion);
        }

        public async Task<IActionResult> Ranking(string? periodo)
        {
            periodo ??= PeriodoHelper.ObtenerPeriodoActual();
            var finales = await _context.Evaluaciones.AsNoTracking()
                .Where(e => e.Estado == EstadoEvaluacion.Final && e.Periodo == periodo)
                .OrderBy(e => e.SnapshotCarrera).ThenByDescending(e => e.NotaCatrina).ThenBy(e => e.SnapshotNombreInscripcion)
                .ToListAsync();

            ViewBag.Nav = this.Nav("Ranking");
            ViewBag.Periodo = periodo;
            return View(finales);
        }

        private IActionResult Volver(int inscripcionId, string error)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Evaluar), new { inscripcionId });
        }

        // rowversion de SQL Server = 8 bytes; cualquier otra cosa se considera mal formada.
        private static byte[]? DecodificarRowVersion(string? valor)
        {
            if (string.IsNullOrEmpty(valor)) return null;
            var bytes = new byte[8];
            return Convert.TryFromBase64String(valor, bytes, out var escritos) && escritos == 8 ? bytes : null;
        }

        private Task<Inscripcion?> CargarInscripcionAsync(int inscripcionId) =>
            _context.Inscripciones
                .Include(i => i.Carrera)
                .Include(i => i.MaestroEncargado)
                .Include(i => i.Evaluacion).ThenInclude(e => e!.Integrantes)
                .Include(i => i.Miembros).ThenInclude(m => m.Registrante)
                .AsSplitQuery()
                .FirstOrDefaultAsync(i => i.Id == inscripcionId && i.Activo);

        private static ConfiguracionPeriodo PesosDe(EvaluacionCatrina e) => new()
        {
            Periodo = e.Periodo,
            PesoVestuario = e.PesoVestuario,
            PesoMaquillaje = e.PesoMaquillaje,
            PesoAccesorios = e.PesoAccesorios,
            PesoOriginalidadCreatividad = e.PesoOriginalidadCreatividad,
            PesoApegoTradiciones = e.PesoApegoTradiciones
        };
    }

    public static class NavExtensions
    {
        public static NavContext Nav(this Controller c, string activo) => new()
        {
            Activo = activo,
            NombreCompleto = c.HttpContext.Session.GetString(Sesion.JuezNombre) ?? "Jurado",
            EsAdmin = c.HttpContext.Session.EsAdmin()
        };
    }
}
