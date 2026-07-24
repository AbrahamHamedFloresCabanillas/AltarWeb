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
        private readonly ILogger<AltarAdminController> _logger;
        private readonly IWebHostEnvironment _entorno;

        // Tope de 25 MB para el recorrido en PDF; suficiente para un mapa por carrera sin permitir abusos.
        private const long RecorridoMaxBytes = 25 * 1024 * 1024;

        public AltarAdminController(AltarDbContext context, ReportePeriodoService reportePeriodo, ILogger<AltarAdminController> logger, IWebHostEnvironment entorno)
        {
            _context = context;
            _reportePeriodo = reportePeriodo;
            _logger = logger;
            _entorno = entorno;
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
            return View(new CrearJuezViewModel());
        }

        [HttpPost]
        public IActionResult CrearJuez(CrearJuezViewModel model)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            if (!string.IsNullOrWhiteSpace(model.Usuario) && _context.Jueces.IgnoreQueryFilters().Any(j => j.Usuario == model.Usuario))
            {
                ModelState.AddModelError(nameof(model.Usuario), "Ese usuario ya existe.");
            }
            if (model.Rol != "Admin" && model.Rol != "Juez") model.Rol = "Juez";

            if (!ModelState.IsValid)
            {
                ViewBag.Nav = ObtenerNavContext("Jueces");
                return View(model);
            }

            // SEC-12: mapeo manual campo por campo (en vez de bindear la entidad EF directo) — Id,
            // IsDeleted, FechaEliminado, CorreoInstitucional y ProveedorAuth quedan fuera del alcance del
            // formulario y se fijan aqui explicitamente, como el resto del proyecto ya hace con ViewModels.
            var juez = new Juez
            {
                NombreCompleto = model.NombreCompleto.Trim(),
                Usuario = model.Usuario.Trim(),
                Password = AccesoController.HashPassword(model.Password),
                Rol = model.Rol,
                ProveedorAuth = "Local",
                Pendiente = false
            };

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

            // Validacion del PDF del recorrido (solo si el admin adjunto uno): extension, tipo y tamaño.
            var archivo = model.ArchivoRecorrido;
            if (archivo != null && archivo.Length > 0)
            {
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (extension != ".pdf" || !EsContenidoPdf(archivo))
                {
                    ModelState.AddModelError(nameof(model.ArchivoRecorrido), "El recorrido debe ser un archivo PDF.");
                }
                else if (archivo.Length > RecorridoMaxBytes)
                {
                    ModelState.AddModelError(nameof(model.ArchivoRecorrido), "El PDF no puede pesar más de 25 MB.");
                }
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

            if (archivo != null && archivo.Length > 0)
            {
                config.RecorridoPdf = await GuardarRecorridoAsync(archivo, periodo);
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
            var generadoPor = HttpContext.Session.GetString("JuezNombre") ?? "Administrador";
            var pdf = _reportePeriodo.GenerarPdf(vm, generadoPor);
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

        [HttpPost]
        public async Task<IActionResult> QuitarRecorrido()
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var config = await _context.ConfiguracionesPeriodo.FirstOrDefaultAsync(c => c.Periodo == periodo);
            if (config != null && !string.IsNullOrEmpty(config.RecorridoPdf))
            {
                EliminarArchivoFisico(config.RecorridoPdf);
                config.RecorridoPdf = null;
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = "Recorrido en PDF eliminado.";
            }
            return RedirectToAction("Configuracion");
        }

        // Guarda el PDF en wwwroot/uploads/recorridos con un nombre estable por periodo (se sobrescribe
        // al re-subir) y devuelve la ruta web servible que se persiste en ConfiguracionPeriodo.RecorridoPdf.
        private async Task<string> GuardarRecorridoAsync(IFormFile archivo, string periodo)
        {
            var webRoot = _entorno.WebRootPath ?? Path.Combine(_entorno.ContentRootPath, "wwwroot");
            var carpeta = Path.Combine(webRoot, "uploads", "recorridos");
            Directory.CreateDirectory(carpeta);

            var nombreArchivo = $"recorrido-{periodo}.pdf";
            var rutaFisica = Path.Combine(carpeta, nombreArchivo);

            using (var stream = new FileStream(rutaFisica, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            return $"/uploads/recorridos/{nombreArchivo}";
        }

        private void EliminarArchivoFisico(string rutaWeb)
        {
            try
            {
                var webRoot = _entorno.WebRootPath ?? Path.Combine(_entorno.ContentRootPath, "wwwroot");
                var rutaFisica = Path.Combine(webRoot, rutaWeb.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(rutaFisica)) System.IO.File.Delete(rutaFisica);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "No se pudo eliminar el archivo de recorrido {Ruta}.", rutaWeb);
            }
        }

        // Verifica la firma %PDF al inicio del archivo (mas confiable que el Content-Type del cliente).
        private static bool EsContenidoPdf(IFormFile archivo)
        {
            try
            {
                using var stream = archivo.OpenReadStream();
                Span<byte> encabezado = stackalloc byte[4];
                var leidos = stream.Read(encabezado);
                return leidos == 4 && encabezado[0] == 0x25 && encabezado[1] == 0x50
                    && encabezado[2] == 0x44 && encabezado[3] == 0x46; // "%PDF"
            }
            catch (IOException)
            {
                return false;
            }
        }

        // decimal(5,4) en BD conserva la escala (30.0000); esto la normaliza para mostrarla limpia (30).
        private static decimal NormalizarPorcentaje(decimal pesoFraccion) =>
            decimal.Parse((pesoFraccion * 100).ToString("0.##"));

        private bool EsAdmin() =>
            HttpContext.Session.GetInt32("JuezId") != null && HttpContext.Session.GetString("JuezRol") == "Admin";

        private IActionResult RedirigirSinPermisos()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");

            // LOG-01: sesion valida pero sin rol Admin intentando una accion de backoffice.
            _logger.LogWarning("Acceso denegado (rol insuficiente). JuezId: {JuezId}, Rol: {Rol}, Ruta: {Ruta}, IP: {IP}",
                HttpContext.Session.GetInt32("JuezId"), HttpContext.Session.GetString("JuezRol"),
                HttpContext.Request.Path, HttpContext.Connection.RemoteIpAddress);

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
