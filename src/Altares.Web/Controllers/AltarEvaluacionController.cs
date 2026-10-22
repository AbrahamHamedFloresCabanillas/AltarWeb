using AltarWeb.Models;
using AltarWeb.Models.Registro;
using AltarWeb.Services;
using AltarWeb.ViewModels.Altar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Equipo = AltarWeb.Models.Registro.Equipo;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace AltarWeb.Controllers
{
    public class AltarEvaluacionController : Controller
    {
        private readonly AltarDbContext _context;
        private readonly EvaluacionCalculoService _calculo;

        public AltarEvaluacionController(AltarDbContext context, EvaluacionCalculoService calculo)
        {
            _context = context;
            _calculo = calculo;
        }

        public async Task<IActionResult> Historial()
        {
            if (!EstaAutenticado()) return RedirectToAction("Login", "Acceso");
            ViewBag.Nav = ObtenerNavContext("Historial");

            var evaluaciones = await _context.EvaluacionesConcurso
                .Include(e => e.Equipo).ThenInclude(eq => eq.Carrera)
                .Include(e => e.Equipo).ThenInclude(eq => eq.Difunto)
                .OrderByDescending(e => e.Periodo)
                .ToListAsync();

            var periodoActual = PeriodoHelper.ObtenerPeriodoActual();
            var equiposPendientes = await _context.EquiposConcurso
                .Where(e => e.Periodo == periodoActual && e.Evaluacion == null)
                .CountAsync();

            var finales = evaluaciones.Where(e => e.Estado == EstadoEvaluacion.Final && e.NotaFinal != null).ToList();

            var vm = new HistorialViewModel
            {
                TotalEvaluaciones = evaluaciones.Count,
                CerradasFinal = evaluaciones.Count(e => e.Estado == EstadoEvaluacion.Final),
                PromedioFinales = finales.Count > 0 ? finales.Average(e => e.NotaFinal!.Value) : null,
                EquiposPendientes = equiposPendientes,
                Periodos = evaluaciones
                    .GroupBy(e => e.Periodo)
                    .OrderByDescending(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.Select(e => new HistorialRowViewModel
                    {
                        EvaluacionId = e.Id,
                        NombreEquipo = e.Equipo.Nombre,
                        TipoAltar = e.Equipo.Difunto?.TipoAltar ?? TipoAltar.Tradicional,
                        Niveles = e.Niveles,
                        Carrera = e.Equipo.Carrera.Nombre,
                        Difunto = e.Equipo.Difunto?.Nombre ?? string.Empty,
                        NotaFinal = e.NotaFinal,
                        Lugar = e.Lugar,
                        Estado = e.Estado
                    }).ToList())
            };

            return View(vm);
        }

        public async Task<IActionResult> Detalle(int id)
        {
            if (!EstaAutenticado()) return RedirectToAction("Login", "Acceso");
            ViewBag.Nav = ObtenerNavContext("Historial");

            var eval = await _context.EvaluacionesConcurso
                .Include(e => e.Equipo).ThenInclude(eq => eq.Carrera)
                .Include(e => e.Equipo).ThenInclude(eq => eq.Difunto)
                .Include(e => e.Juez)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (eval == null) return NotFound();

            return View(new EvaluacionDetalleViewModel
            {
                EvaluacionId = eval.Id,
                NombreEquipo = eval.Equipo.Nombre,
                NombreAltar = eval.Equipo.NombreAltar,
                TipoAltar = eval.Equipo.Difunto?.TipoAltar ?? TipoAltar.Tradicional,
                Carrera = eval.Equipo.Carrera.Nombre,
                Difunto = eval.Equipo.Difunto?.Nombre ?? string.Empty,
                Niveles = eval.Niveles,
                Juez = eval.Juez?.NombreCompleto ?? eval.Juez?.Usuario ?? "—",
                ObjetivoCultural = eval.ObjetivoCultural,
                EsenciaPersonalidad = eval.EsenciaPersonalidadFinal ?? eval.EsenciaPersonalidad,
                ValoracionGeneral = eval.ValoracionGeneral,
                DistribucionNiveles = eval.DistribucionNiveles,
                Narrador = eval.Narrador,
                NotaFinal = eval.NotaFinal,
                Lugar = eval.Lugar,
                Estado = eval.Estado
            });
        }

        public async Task<IActionResult> NuevaEvaluacion(int? equipoId, string? q)
        {
            if (!EstaAutenticado()) return RedirectToAction("Login", "Acceso");
            ViewBag.Nav = ObtenerNavContext("NuevaEvaluacion");

            var periodo = PeriodoHelper.ObtenerPeriodoActual();

            var query = _context.EquiposConcurso.Include(e => e.Carrera).Include(e => e.Integrantes)
                .Where(e => e.Periodo == periodo).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var texto = q.ToLower();
                query = query.Where(e => e.Nombre.ToLower().Contains(texto) || e.Carrera.Nombre.ToLower().Contains(texto));
            }

            var equipos = await query.OrderBy(e => e.Nombre).Take(20).ToListAsync();

            var vm = new NuevaEvaluacionViewModel
            {
                EsAdmin = EsAdmin(),
                Busqueda = q ?? string.Empty,
                Resultados = equipos.Select(e => new EquipoBusquedaItemViewModel
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    Carrera = e.Carrera.Nombre,
                    IntegrantesCount = e.Integrantes.Count,
                    YaEvaluado = e.Evaluacion != null
                }).ToList()
            };

            if (equipoId != null)
            {
                var equipo = await _context.EquiposConcurso
                    .Include(e => e.Carrera)
                    .Include(e => e.Difunto)
                    .Include(e => e.MaestroEncargado)
                    .Include(e => e.CreadoPorRegistrante)
                    .Include(e => e.Integrantes).ThenInclude(ri => ri.Registrante)
                    .Include(e => e.Evaluacion).ThenInclude(ev => ev!.ElementosEvaluados)
                    .FirstOrDefaultAsync(e => e.Id == equipoId.Value);

                if (equipo == null) return NotFound();

                var elementos = await _context.Elementos.Where(el => el.Activo).OrderBy(el => el.Orden).ToListAsync();
                var eval = equipo.Evaluacion;

                vm.EquipoId = equipo.Id;
                vm.EvaluacionId = eval?.Id;
                vm.SoloLectura = eval?.Estado == EstadoEvaluacion.Final;
                vm.EsPreliminarExistente = eval?.Estado == EstadoEvaluacion.Preliminar;
                vm.NombreEquipo = equipo.Nombre;
                vm.NombreAltar = equipo.NombreAltar;
                vm.Carrera = equipo.Carrera.Nombre;
                vm.UbicacionAltar = equipo.UbicacionAltar ?? string.Empty;
                vm.ResponsableNombre = equipo.CreadoPorRegistrante?.NombreCompleto ?? string.Empty;
                vm.ResponsableTelefono = equipo.CreadoPorRegistrante?.Telefono ?? string.Empty;
                vm.ResponsableCorreo = equipo.CreadoPorRegistrante?.CorreoInstitucional ?? string.Empty;
                vm.MaestroEncargado = equipo.MaestroEncargado?.NombreCompleto
                    ?? (equipo.MaestroEncargadoIdentificadorPendiente != null ? $"Pendiente ({equipo.MaestroEncargadoIdentificadorPendiente})" : string.Empty);
                vm.Integrantes = equipo.Integrantes.ToList();

                // Misma regla que usa el portal del participante y que exige el cierre como Final.
                vm.FichaCompleta = EquipoValidacionHelper.EsFichaCompleta(equipo);
                vm.FichaFaltantes = EquipoValidacionHelper.FaltantesFicha(equipo);
                vm.FichaValidada = EquipoValidacionHelper.FueValidadaPorAdmin(equipo);
                vm.TieneNarrador = EquipoValidacionHelper.TieneNarradorDesignado(equipo);
                vm.PuedeCerrarFinal = EquipoValidacionHelper.EstaListoParaFinal(equipo, out var motivoNoFinal);
                vm.MotivoNoFinal = motivoNoFinal;

                vm.DifuntoNombre = equipo.Difunto?.Nombre ?? string.Empty;
                vm.DifuntoFechaDefuncion = equipo.Difunto?.FechaDefuncion ?? DateOnly.FromDateTime(DateTime.Today);
                vm.TipoAltar = equipo.Difunto?.TipoAltar ?? TipoAltar.Tradicional;
                vm.SemblanzaHobbiesTematica = equipo.Difunto?.SemblanzaHobbiesTematica ?? string.Empty;
                vm.Niveles = eval?.Niveles ?? 7;

                var elementosPorId = eval?.ElementosEvaluados.ToDictionary(ee => ee.ElementoId) ?? new Dictionary<int, ElementoEvaluado>();

                vm.Elementos = elementos.Select(el =>
                {
                    elementosPorId.TryGetValue(el.Id, out var ee);
                    var nivelSugerido = vm.Niveles == 3 ? el.NivelSugerido3 : el.NivelSugerido7;
                    return new ElementoChecklistItemViewModel
                    {
                        ElementoId = el.Id,
                        Nombre = el.Nombre,
                        Categoria = el.Categoria,
                        NivelSugerido = nivelSugerido,
                        NivelAsignado = ee?.NivelAsignado ?? nivelSugerido,
                        Significado = el.Significado,
                        Colocacion = el.Colocacion,
                        Satisfaccion = ee?.Satisfaccion ?? Satisfaccion.NoLoTiene,
                        Tematizado = ee?.Tematizado ?? false
                    };
                }).OrderBy(el => el.Nombre).ToList();

                vm.PuntajeElementos = eval?.PuntajeElementos ?? 0;
                vm.ObjetivoCultural = eval?.ObjetivoCultural ?? 0;
                vm.EsenciaPersonalidad = eval?.EsenciaPersonalidad ?? 0;
                vm.ValoracionGeneral = eval?.ValoracionGeneral ?? 0;
                vm.DistribucionNiveles = eval?.DistribucionNiveles ?? 0;
                vm.Narrador = eval?.Narrador ?? 0;
                vm.NotaFinalPreview = eval?.NotaFinal ?? 0;

            }

            var config = await ObtenerConfiguracionAsync(periodo);
            vm.PesoObjetivoCultural = config.PesoObjetivoCultural;
            vm.PesoEsenciaPersonalidad = config.PesoEsenciaPersonalidad;
            vm.PesoValoracionGeneral = config.PesoValoracionGeneral;
            vm.PesoDistribucionNiveles = config.PesoDistribucionNiveles;
            vm.PesoNarrador = config.PesoNarrador;
            vm.PesoElementoRitual = config.PesoElementoRitual;
            vm.PesoElementoDecorativo = config.PesoElementoDecorativo;
            vm.ValorSatisfaccionNoLoTiene = config.ValorSatisfaccionNoLoTiene;
            vm.ValorSatisfaccionRegular = config.ValorSatisfaccionRegular;
            vm.ValorSatisfaccionBueno = config.ValorSatisfaccionBueno;
            vm.BonusPorElementoTematizado = config.BonusPorElementoTematizado;

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> GuardarEvaluacion(
            int equipoId, int niveles, string tipoAltar, string semblanza,
            decimal esenciaPersonalidad, decimal valoracionGeneral,
            decimal distribucionNiveles, decimal narrador,
            string accion)
        {
            var juezId = HttpContext.Session.GetInt32("JuezId");
            if (juezId == null) return RedirectToAction("Login", "Acceso");

            var equipo = await _context.EquiposConcurso
                .Include(e => e.Difunto)
                .Include(e => e.Integrantes).ThenInclude(ri => ri.Registrante)
                .Include(e => e.Evaluacion)
                .FirstOrDefaultAsync(e => e.Id == equipoId);

            if (equipo == null) return NotFound();

            if (equipo.Evaluacion?.Estado == EstadoEvaluacion.Final)
            {
                TempData["Error"] = "Esta evaluación ya fue cerrada como Final.";
                return RedirectToAction("Detalle", new { id = equipo.Evaluacion.Id });
            }

            // C1 (pruebas adversariales 2026-10-21): los sliders van de 0 a 10, pero el servidor aceptaba cualquier
            // numero (999 daba nota 405.6 y 1er lugar; 1000 desbordaba decimal(5,2) con un 500). Misma regla que Catrinas.
            // Un valor no numerico (formulario alterado) llega como 0 con ModelState invalido: tambien se rechaza.
            // Se revisa campo por campo y no ModelState.IsValid: una semblanza vacia lo invalidaria sin ser un error.
            var criterios = new (string Campo, decimal Valor)[]
            {
                (nameof(esenciaPersonalidad), esenciaPersonalidad), (nameof(valoracionGeneral), valoracionGeneral),
                (nameof(distribucionNiveles), distribucionNiveles), (nameof(narrador), narrador)
            };
            if (criterios.Any(c => c.Valor < 0 || c.Valor > 10 || ModelState[c.Campo]?.ValidationState == ModelValidationState.Invalid))
            {
                TempData["Error"] = "Cada criterio debe calificarse entre 0 y 10.";
                return RedirectToAction("NuevaEvaluacion", new { equipoId = equipo.Id });
            }

            // SEC-13: vision.md §4.2 exige un Narrador por equipo; una evaluacion Final tambien implica
            // que la ficha (incluido maestro encargado) esta completa. La designacion de narrador ya se
            // fuerza auto-exclusiva en RegistroController.DesignarNarrador, pero nada impedia cerrar
            // Final sin haber pasado por ahi. La regla vive en EquipoValidacionHelper.
            //
            // Ojo: la validacion administrativa de la ficha (Equipo.FichaValidadaPorAdminEn) NO relaja
            // esto. Es a proposito: el snapshot de la evaluacion y las constancias necesitan los datos
            // reales, y marcar la ficha como revisada no los inventa.
            if (accion == "final" && !EquipoValidacionHelper.EstaListoParaFinal(equipo, out var motivoFinal))
            {
                TempData["Error"] = $"No se puede cerrar como Final: {motivoFinal}.";
                return RedirectToAction("NuevaEvaluacion", new { equipoId = equipo.Id });
            }

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var config = await ObtenerConfiguracionAsync(periodo);

            var esNueva = equipo.Evaluacion == null;
            var eval = equipo.Evaluacion ?? new Evaluacion { EquipoId = equipo.Id };
            if (esNueva) _context.EvaluacionesConcurso.Add(eval);

            eval.JuezId = juezId.Value;
            eval.Periodo = periodo;
            eval.Niveles = niveles == 3 ? 3 : 7;
            eval.EsenciaPersonalidad = esenciaPersonalidad;
            eval.ValoracionGeneral = valoracionGeneral;
            eval.DistribucionNiveles = distribucionNiveles;
            eval.Narrador = narrador;
            eval.SnapshotNombreEquipo = equipo.Nombre;
            eval.SnapshotNombreAltar = equipo.NombreAltar;
            eval.SnapshotDifuntoNombre = equipo.Difunto?.Nombre ?? string.Empty;
            eval.SnapshotDifuntoFechaDefuncion = equipo.Difunto?.FechaDefuncion;
            eval.ActualizadoEn = DateTime.Now;
            eval.Estado = accion == "final" ? EstadoEvaluacion.Final : EstadoEvaluacion.Preliminar;

            if (equipo.Difunto != null)
            {
                equipo.Difunto.TipoAltar = tipoAltar == "Ninos" ? TipoAltar.Ninos : TipoAltar.Tradicional;
                equipo.Difunto.SemblanzaHobbiesTematica = semblanza;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            // SEC-15: dos jueces evaluando el mismo equipo a la vez; ambos pasaron el check
            // "equipo.Evaluacion == null" y el segundo Add colisiona con el indice unico de
            // Evaluacion.EquipoId. Confirmado con una prueba real de dos requests concurrentes: el
            // "perdedor" no siempre lanza DbUpdateException (violacion SQL) — a veces EF Core lanza antes
            // InvalidOperationException ("la asociacion Equipo/Evaluacion fue cortada") al reconciliar el
            // grafo de cambios para la relacion requerida Equipo-Evaluacion. Se capturan ambos casos.
            catch (Exception ex) when (esNueva && (ex is DbUpdateException || ex is InvalidOperationException))
            {
                // En vez de un 500 crudo, mandamos al juez al detalle de la evaluacion que ya quedo guardada.
                var existente = await _context.EvaluacionesConcurso.AsNoTracking().FirstOrDefaultAsync(e => e.EquipoId == equipoId);
                TempData["Error"] = "Otro juez ya registró una evaluación para este equipo mientras guardabas la tuya.";
                return existente != null
                    ? RedirectToAction("Detalle", new { id = existente.Id })
                    : RedirectToAction("Historial");
            }

            // Checklist de elementos: los campos llegan como Satisfaccion_{id}, Nivel_{id},
            // Tematizado_{id} (uno por cada elemento activo del catalogo).
            var elementos = await _context.Elementos.Where(el => el.Activo).ToListAsync();
            var existentes = await _context.ElementosEvaluados.Where(ee => ee.EvaluacionId == eval.Id).ToListAsync();

            foreach (var elemento in elementos)
            {
                var campoSatisfaccion = Request.Form["Satisfaccion_" + elemento.Id];
                if (!Enum.TryParse<Satisfaccion>(campoSatisfaccion, out var satisfaccion)) satisfaccion = Satisfaccion.NoLoTiene;

                var campoNivel = Request.Form["Nivel_" + elemento.Id];
                int? nivelAsignado = int.TryParse(campoNivel, out var nivelParsed) ? nivelParsed : null;

                // Tematizado solo aplica si el elemento esta presente en alguna medida.
                var tematizado = satisfaccion != Satisfaccion.NoLoTiene && Request.Form["Tematizado_" + elemento.Id] == "true";

                var existente = existentes.FirstOrDefault(ee => ee.ElementoId == elemento.Id);
                if (existente != null)
                {
                    existente.Satisfaccion = satisfaccion;
                    existente.NivelAsignado = nivelAsignado;
                    existente.Tematizado = tematizado;
                }
                else
                {
                    _context.ElementosEvaluados.Add(new ElementoEvaluado
                    {
                        EvaluacionId = eval.Id,
                        ElementoId = elemento.Id,
                        Satisfaccion = satisfaccion,
                        NivelAsignado = nivelAsignado,
                        Tematizado = tematizado
                    });
                }
            }

            await _context.SaveChangesAsync();

            if (esNueva)
            {
                foreach (var integrante in equipo.Integrantes)
                {
                    _context.EvaluacionIntegrantes.Add(new EvaluacionIntegrante
                    {
                        EvaluacionId = eval.Id,
                        RegistranteId = integrante.RegistranteId,
                        NombreCompleto = integrante.Registrante.NombreCompleto,
                        Identificador = integrante.Registrante.Identificador,
                        Rol = integrante.Rol
                    });
                }
            }

            var elementosEvaluados = await _context.ElementosEvaluados
                .Include(ee => ee.Elemento)
                .Where(ee => ee.EvaluacionId == eval.Id)
                .ToListAsync();

            eval.PuntajeElementos = _calculo.CalcularPuntajeElementos(elementosEvaluados, config);
            // Objetivo Cultural ya no lo captura el juez a mano: se sincroniza con el
            // Puntaje de Elementos (decision del comite).
            eval.ObjetivoCultural = eval.PuntajeElementos;

            var cantidadTematizados = elementosEvaluados.Count(ee => ee.Tematizado);
            eval.EsenciaPersonalidadFinal = _calculo.CalcularEsenciaPersonalidadFinal(eval.EsenciaPersonalidad, cantidadTematizados, config);

            eval.NotaFinal = _calculo.CalcularNotaFinal(eval, config);

            await _context.SaveChangesAsync();

            if (accion == "final")
            {
                await _calculo.RecalcularLugaresAsync(equipo.CarreraId, periodo);
                TempData["Mensaje"] = "Evaluación cerrada como Final.";
                return RedirectToAction("Detalle", new { id = eval.Id });
            }

            TempData["Mensaje"] = "Evaluación guardada como Preliminar.";
            return RedirectToAction("NuevaEvaluacion", new { equipoId = equipo.Id });
        }

        public async Task<IActionResult> RecorridoPdf()
        {
            if (!EstaAutenticado()) return RedirectToAction("Login", "Acceso");
            ViewBag.Nav = ObtenerNavContext("RecorridoPdf");

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var config = await ObtenerConfiguracionAsync(periodo);

            var avance = await _context.EquiposConcurso
                .Include(e => e.Carrera)
                .Where(e => e.Periodo == periodo)
                .GroupBy(e => e.Carrera.Nombre)
                .Select(g => new AvanceCarreraViewModel
                {
                    Carrera = g.Key,
                    Total = g.Count(),
                    Finales = g.Count(e => e.Evaluacion != null && e.Evaluacion.Estado == EstadoEvaluacion.Final),
                    Preliminares = g.Count(e => e.Evaluacion != null && e.Evaluacion.Estado == EstadoEvaluacion.Preliminar)
                })
                .OrderBy(a => a.Carrera)
                .ToListAsync();

            ViewBag.RecorridoPdf = config.RecorridoPdf;
            ViewBag.Avance = avance;

            return View();
        }

        private async Task<ConfiguracionPeriodo> ObtenerConfiguracionAsync(string periodo)
        {
            var config = await _context.ConfiguracionesPeriodo.FirstOrDefaultAsync(c => c.Periodo == periodo);
            return config ?? new ConfiguracionPeriodo { Periodo = periodo };
        }

        private bool EstaAutenticado() => HttpContext.Session.GetInt32("JuezId") != null;

        private bool EsAdmin() =>
            HttpContext.Session.GetInt32("JuezId") != null && HttpContext.Session.GetString("JuezRol") == "Admin";

        internal AltarWeb.ViewModels.Altar.AltarNavContext ObtenerNavContext(string activeItem)
        {
            return new AltarWeb.ViewModels.Altar.AltarNavContext
            {
                ActiveItem = activeItem,
                NombreCompleto = HttpContext.Session.GetString("JuezNombre") ?? "Usuario",
                Rol = HttpContext.Session.GetString("JuezRol") ?? "Juez",
                EsAdmin = HttpContext.Session.GetString("JuezRol") == "Admin"
            };
        }
    }
}
