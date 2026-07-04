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
    public class AltarAdminController : Controller
    {
        private readonly AltarDbContext _context;
        private readonly ReportePeriodoService _reportePeriodo;

        public AltarAdminController(AltarDbContext context, ReportePeriodoService reportePeriodo)
        {
            _context = context;
            _reportePeriodo = reportePeriodo;
        }

        // --- Jueces (nuevo controlador/vistas sobre el modelo legado Juez; no toca JuecesController) ---

        public IActionResult Jueces()
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            ViewBag.Nav = ObtenerNavContext("Jueces");

            var jueces = _context.Jueces.OrderBy(j => j.NombreCompleto).ToList();
            return View(jueces);
        }

        public IActionResult CrearJuez()
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            ViewBag.Nav = ObtenerNavContext("Jueces");
            return View(new Juez());
        }

        [HttpPost]
        public IActionResult CrearJuez(Juez juez)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            if (!string.IsNullOrWhiteSpace(juez.Usuario) && _context.Jueces.IgnoreQueryFilters().Any(j => j.Usuario == juez.Usuario))
            {
                ModelState.AddModelError(nameof(juez.Usuario), "Ese usuario ya existe.");
            }
            if (juez.Rol != "Admin" && juez.Rol != "Juez") juez.Rol = "Juez";

            if (!ModelState.IsValid)
            {
                ViewBag.Nav = ObtenerNavContext("Jueces");
                return View(juez);
            }

            juez.Password = AccesoController.HashPassword(juez.Password);
            juez.ProveedorAuth = "Local";
            juez.Pendiente = false;

            _context.Jueces.Add(juez);
            _context.SaveChanges();
            TempData["Mensaje"] = $"Usuario '{juez.Usuario}' creado.";
            return RedirectToAction("Jueces");
        }

        [HttpPost]
        public IActionResult AprobarJuez(int id)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            var juez = _context.Jueces.Find(id);
            if (juez != null)
            {
                juez.Pendiente = false;
                _context.SaveChanges();
                TempData["Mensaje"] = $"'{juez.NombreCompleto}' fue aprobado y ya puede iniciar sesión.";
            }
            return RedirectToAction("Jueces");
        }

        [HttpPost]
        public IActionResult DesactivarJuez(int id)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            var juezActual = HttpContext.Session.GetInt32("JuezId");
            if (id == juezActual)
            {
                TempData["Error"] = "No puedes desactivar tu propia cuenta.";
                return RedirectToAction("Jueces");
            }

            var juez = _context.Jueces.Find(id);
            if (juez != null)
            {
                juez.IsDeleted = true;
                juez.FechaEliminado = DateTime.UtcNow;
                _context.SaveChanges();
                TempData["Mensaje"] = $"'{juez.Usuario}' desactivado.";
            }
            return RedirectToAction("Jueces");
        }

        [HttpPost]
        public IActionResult ReactivarJuez(int id)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            var juez = _context.Jueces.IgnoreQueryFilters().FirstOrDefault(j => j.Id == id);
            if (juez != null)
            {
                juez.IsDeleted = false;
                juez.FechaEliminado = null;
                _context.SaveChanges();
                TempData["Mensaje"] = $"'{juez.Usuario}' reactivado.";
            }
            return RedirectToAction("Jueces");
        }

        // --- Registrantes y Equipos ---

        public async Task<IActionResult> RegistrantesYEquipos(string tab = "registrantes")
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            ViewBag.Nav = ObtenerNavContext("RegistrantesYEquipos");
            ViewBag.Tab = tab;

            if (tab == "equipos")
            {
                var periodo = PeriodoHelper.ObtenerPeriodoActual();
                var equipos = await _context.EquiposConcurso
                    .Include(e => e.Carrera)
                    .Include(e => e.MaestroEncargado)
                    .Include(e => e.Difunto)
                    .Include(e => e.Integrantes)
                    .Include(e => e.Evaluacion)
                    .Where(e => e.Periodo == periodo)
                    .OrderBy(e => e.Nombre)
                    .Select(e => new EquipoAdminCardViewModel
                    {
                        Id = e.Id,
                        Nombre = e.Nombre,
                        NombreAltar = e.NombreAltar,
                        Carrera = e.Carrera.Nombre,
                        IntegrantesCount = e.Integrantes.Count,
                        Periodo = e.Periodo,
                        MaestroEncargado = e.MaestroEncargado != null ? e.MaestroEncargado.NombreCompleto : e.MaestroEncargadoIdentificadorPendiente,
                        UbicacionAltar = e.UbicacionAltar,
                        Evaluado = e.Evaluacion != null,
                        FichaCompleta = !string.IsNullOrEmpty(e.NombreAltar) && !string.IsNullOrEmpty(e.UbicacionAltar) && e.Difunto != null && e.MaestroEncargadoId != null
                    })
                    .ToListAsync();

                return View("RegistrantesYEquiposEquipos", equipos);
            }

            var registrantes = await _context.Registrantes
                .IgnoreQueryFilters()
                .Include(r => r.CatalogoCarrera)
                .Include(r => r.CatalogoGenero)
                .OrderBy(r => r.NombreCompleto)
                .Select(r => new RegistranteAdminRowViewModel
                {
                    Id = r.Id,
                    NombreCompleto = r.NombreCompleto,
                    CorreoInstitucional = r.CorreoInstitucional,
                    Tipo = r.Tipo,
                    Identificador = r.Identificador,
                    Carrera = r.CatalogoCarrera != null ? r.CatalogoCarrera.Nombre : null,
                    Genero = r.CatalogoGenero != null ? r.CatalogoGenero.Nombre : null,
                    Activo = r.Activo
                })
                .ToListAsync();

            return View("RegistrantesYEquiposRegistrantes", registrantes);
        }

        [HttpPost]
        public async Task<IActionResult> DesactivarRegistrante(int id)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            var registrante = await _context.Registrantes.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id);
            if (registrante != null)
            {
                registrante.Activo = false;
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"'{registrante.NombreCompleto}' desactivado.";
            }
            return RedirectToAction("RegistrantesYEquipos", new { tab = "registrantes" });
        }

        [HttpPost]
        public async Task<IActionResult> ReactivarRegistrante(int id)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            var registrante = await _context.Registrantes.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id);
            if (registrante != null)
            {
                registrante.Activo = true;
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"'{registrante.NombreCompleto}' reactivado.";
            }
            return RedirectToAction("RegistrantesYEquipos", new { tab = "registrantes" });
        }

        // --- Configuracion del periodo ---

        public async Task<IActionResult> Configuracion()
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            ViewBag.Nav = ObtenerNavContext("Configuracion");

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var config = await _context.ConfiguracionesPeriodo.FirstOrDefaultAsync(c => c.Periodo == periodo);
            config ??= new ConfiguracionPeriodo { Periodo = periodo };

            var vm = new ConfiguracionPeriodoAdminViewModel
            {
                Periodo = config.Periodo,
                FechaLimiteInscripcion = config.FechaLimiteInscripcion,
                FechaLimiteRequisitos = config.FechaLimiteRequisitos,
                RecorridoPdf = config.RecorridoPdf,
                PesoObjetivoCulturalPct = NormalizarPorcentaje(config.PesoObjetivoCultural),
                PesoEsenciaPersonalidadPct = NormalizarPorcentaje(config.PesoEsenciaPersonalidad),
                PesoValoracionGeneralPct = NormalizarPorcentaje(config.PesoValoracionGeneral),
                PesoDistribucionNivelesPct = NormalizarPorcentaje(config.PesoDistribucionNiveles),
                PesoNarradorPct = NormalizarPorcentaje(config.PesoNarrador),
                UmbralAgrupacionDemografica = config.UmbralAgrupacionDemografica,
                CarrerasCount = await _context.CatalogoCarreras.CountAsync(c => c.Activo),
                GenerosCount = await _context.CatalogoGeneros.CountAsync(g => g.Activo),
                ElementosCount = await _context.Elementos.CountAsync(e => e.Activo)
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Configuracion(ConfiguracionPeriodoAdminViewModel model)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            if (model.SumaPesos != 100)
            {
                ModelState.AddModelError(string.Empty, "Los pesos deben sumar 100%.");
            }

            // PRIV-01: validacion server-side explicita ademas del [Range] del ViewModel, para que el
            // piso minimo no dependa exclusivamente de la validacion de binding.
            if (model.UmbralAgrupacionDemografica < PrivacidadReporteHelper.UmbralMinimo)
            {
                ModelState.AddModelError(nameof(model.UmbralAgrupacionDemografica),
                    $"El umbral de agrupación demográfica no puede ser menor a {PrivacidadReporteHelper.UmbralMinimo}.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Nav = ObtenerNavContext("Configuracion");
                model.CarrerasCount = await _context.CatalogoCarreras.CountAsync(c => c.Activo);
                model.GenerosCount = await _context.CatalogoGeneros.CountAsync(g => g.Activo);
                model.ElementosCount = await _context.Elementos.CountAsync(e => e.Activo);
                return View(model);
            }

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var config = await _context.ConfiguracionesPeriodo.FirstOrDefaultAsync(c => c.Periodo == periodo);
            if (config == null)
            {
                config = new ConfiguracionPeriodo { Periodo = periodo };
                _context.ConfiguracionesPeriodo.Add(config);
            }

            config.FechaLimiteInscripcion = model.FechaLimiteInscripcion;
            config.FechaLimiteRequisitos = model.FechaLimiteRequisitos;
            config.PesoObjetivoCultural = model.PesoObjetivoCulturalPct / 100;
            config.PesoEsenciaPersonalidad = model.PesoEsenciaPersonalidadPct / 100;
            config.PesoValoracionGeneral = model.PesoValoracionGeneralPct / 100;
            config.PesoDistribucionNiveles = model.PesoDistribucionNivelesPct / 100;
            config.PesoNarrador = model.PesoNarradorPct / 100;
            config.UmbralAgrupacionDemografica = model.UmbralAgrupacionDemografica;

            await _context.SaveChangesAsync();
            TempData["Mensaje"] = "Configuración del periodo actualizada.";
            return RedirectToAction("Configuracion");
        }

        // --- Reporte de Cierre de Periodo (vision.md seccion 12) ---

        public async Task<IActionResult> ReportePeriodo(string? periodo)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            ViewBag.Nav = ObtenerNavContext("ReportePeriodo");

            var vm = await ConstruirReportePeriodoAsync(periodo ?? PeriodoHelper.ObtenerPeriodoActual());
            return View(vm);
        }

        public async Task<IActionResult> ReportePeriodoPdf(string? periodo)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var vm = await ConstruirReportePeriodoAsync(periodo ?? PeriodoHelper.ObtenerPeriodoActual());
            var pdf = _reportePeriodo.GenerarPdf(vm);
            return File(pdf, "application/pdf", $"ReporteCierre_{vm.Periodo}.pdf");
        }

        private async Task<ReportePeriodoViewModel> ConstruirReportePeriodoAsync(string periodo)
        {
            var vm = await _reportePeriodo.GenerarReporteAsync(periodo);

            var registrantesEnPeriodo = await _context.EquiposConcurso
                .Where(e => e.Periodo == periodo)
                .Include(e => e.Integrantes).ThenInclude(ri => ri.Registrante).ThenInclude(r => r!.CatalogoCarrera)
                .SelectMany(e => e.Integrantes)
                .Select(ri => ri.Registrante)
                .ToListAsync();
            vm.DistribucionAcademica.RegistrantesAlumnosPorCarrera =
                ReportePeriodoService.CalcularAlumnosPorCarrera(registrantesEnPeriodo.DistinctBy(r => r.Id).ToList());

            return vm;
        }

        // decimal(5,4) en BD conserva la escala (30.0000); esto la normaliza para mostrarla limpia (30).
        private static decimal NormalizarPorcentaje(decimal pesoFraccion) =>
            decimal.Parse((pesoFraccion * 100).ToString("0.##"));

        private bool EsAdmin() =>
            HttpContext.Session.GetInt32("JuezId") != null && HttpContext.Session.GetString("JuezRol") == "Admin";

        private IActionResult RedirigirSinPermisos()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            TempData["Error"] = "No tienes permisos para acceder a esta sección.";
            return RedirectToAction("Historial", "AltarEvaluacion");
        }

        private AltarWeb.ViewModels.Altar.AltarNavContext ObtenerNavContext(string activeItem)
        {
            return new AltarWeb.ViewModels.Altar.AltarNavContext
            {
                ActiveItem = activeItem,
                NombreCompleto = HttpContext.Session.GetString("JuezNombre") ?? "Usuario",
                Rol = HttpContext.Session.GetString("JuezRol") ?? "Juez",
                EsAdmin = true
            };
        }
    }
}
