using AltarWeb.Models;
using AltarWeb.Models.Registro;
using AltarWeb.Services;
using AltarWeb.ViewModels.Altar;
using Microsoft.AspNetCore.Mvc;
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
                .Include(e => e.EvaluacionCatrina)
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
                Estado = eval.Estado,
                IncluyeCatrina = eval.IncluyeCatrina,
                NotaCatrina = eval.EvaluacionCatrina?.NotaCatrina
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
                    .Include(e => e.Evaluacion).ThenInclude(ev => ev!.EvaluacionCatrina)
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
                        Satisfaccion = ee?.Satisfaccion ?? Satisfaccion.NoPresente,
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

                vm.HaceCatrina = equipo.HaceCatrina;
                vm.SombreroTocado = eval?.EvaluacionCatrina?.SombreroTocado ?? 0;
                vm.Guantes = eval?.EvaluacionCatrina?.Guantes ?? 0;
                vm.Vestimenta = eval?.EvaluacionCatrina?.Vestimenta ?? 0;
                vm.Zapatos = eval?.EvaluacionCatrina?.Zapatos ?? 0;
                vm.Collar = eval?.EvaluacionCatrina?.Collar ?? 0;
                vm.Maquillaje = eval?.EvaluacionCatrina?.Maquillaje ?? 0;
            }

            var config = await ObtenerConfiguracionAsync(periodo);
            vm.PesoObjetivoCultural = config.PesoObjetivoCultural;
            vm.PesoEsenciaPersonalidad = config.PesoEsenciaPersonalidad;
            vm.PesoValoracionGeneral = config.PesoValoracionGeneral;
            vm.PesoDistribucionNiveles = config.PesoDistribucionNiveles;
            vm.PesoNarrador = config.PesoNarrador;
            vm.PesoElementoRitual = config.PesoElementoRitual;
            vm.PesoElementoDecorativo = config.PesoElementoDecorativo;
            vm.ValorSatisfaccionNoPresente = config.ValorSatisfaccionNoPresente;
            vm.ValorSatisfaccionPoco = config.ValorSatisfaccionPoco;
            vm.ValorSatisfaccionSatisfactorio = config.ValorSatisfaccionSatisfactorio;
            vm.ValorSatisfaccionMuySatisfactorio = config.ValorSatisfaccionMuySatisfactorio;
            vm.BonusPorElementoTematizado = config.BonusPorElementoTematizado;

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> GuardarEvaluacion(
            int equipoId, int niveles, string tipoAltar, string semblanza,
            decimal esenciaPersonalidad, decimal valoracionGeneral,
            decimal distribucionNiveles, decimal narrador,
            decimal sombreroTocado, decimal guantes, decimal vestimenta,
            decimal zapatos, decimal collar, decimal maquillaje,
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
            eval.IncluyeCatrina = equipo.HaceCatrina;
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

            await _context.SaveChangesAsync();

            // Checklist de elementos: los campos llegan como Satisfaccion_{id}, Nivel_{id},
            // Tematizado_{id} (uno por cada uno de los 21 elementos).
            var elementos = await _context.Elementos.Where(el => el.Activo).ToListAsync();
            var existentes = await _context.ElementosEvaluados.Where(ee => ee.EvaluacionId == eval.Id).ToListAsync();

            foreach (var elemento in elementos)
            {
                var campoSatisfaccion = Request.Form["Satisfaccion_" + elemento.Id];
                if (!Enum.TryParse<Satisfaccion>(campoSatisfaccion, out var satisfaccion)) satisfaccion = Satisfaccion.NoPresente;

                var campoNivel = Request.Form["Nivel_" + elemento.Id];
                int? nivelAsignado = int.TryParse(campoNivel, out var nivelParsed) ? nivelParsed : null;

                // Tematizado solo aplica si el elemento esta presente en alguna medida.
                var tematizado = satisfaccion != Satisfaccion.NoPresente && Request.Form["Tematizado_" + elemento.Id] == "true";

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

            if (equipo.HaceCatrina)
            {
                var catrina = await _context.EvaluacionesCatrina.FirstOrDefaultAsync(c => c.EvaluacionId == eval.Id);
                if (catrina == null)
                {
                    catrina = new EvaluacionCatrina { EvaluacionId = eval.Id };
                    _context.EvaluacionesCatrina.Add(catrina);
                }
                catrina.SombreroTocado = sombreroTocado;
                catrina.Guantes = guantes;
                catrina.Vestimenta = vestimenta;
                catrina.Zapatos = zapatos;
                catrina.Collar = collar;
                catrina.Maquillaje = maquillaje;
                catrina.NotaCatrina = _calculo.CalcularNotaCatrina(catrina);
            }

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
                .Select(g => new
                {
                    Carrera = g.Key,
                    Total = g.Count(),
                    Calificados = g.Count(e => e.Evaluacion != null && e.Evaluacion.Estado == EstadoEvaluacion.Final)
                })
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
