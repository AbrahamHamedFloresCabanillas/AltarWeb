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

        // --- Jueces (vistas de backoffice sobre la entidad Juez, que sigue siendo la tabla de auth) ---

        public IActionResult Jueces()
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            ViewBag.Nav = ObtenerNavContext("Jueces");

            // IgnoreQueryFilters: el filtro global de Juez es !IsDeleted, asi que sin esto los
            // desactivados no aparecen y el boton "Reactivar" de la vista queda inalcanzable
            // (mismo criterio que RegistrantesYEquipos, que ya listaba inactivos).
            var jueces = _context.Jueces.IgnoreQueryFilters().OrderBy(j => j.NombreCompleto).ToList();
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

        // Edicion de un juez/admin (vision.md 11: "edicion, promocion a admin"). Al igual que
        // CrearJuez, se mapea campo por campo desde un ViewModel; Id/IsDeleted/Pendiente/ProveedorAuth
        // nunca vienen del formulario.
        public IActionResult EditarJuez(int id)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var juez = _context.Jueces.IgnoreQueryFilters().FirstOrDefault(j => j.Id == id);
            if (juez == null)
            {
                TempData["Error"] = "El usuario ya no existe.";
                return RedirectToAction("Jueces");
            }

            ViewBag.Nav = ObtenerNavContext("Jueces");
            return View(ConstruirEditarJuezViewModel(juez));
        }

        [HttpPost]
        public IActionResult EditarJuez(EditarJuezViewModel model)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var juez = _context.Jueces.IgnoreQueryFilters().FirstOrDefault(j => j.Id == model.Id);
            if (juez == null)
            {
                TempData["Error"] = "El usuario ya no existe.";
                return RedirectToAction("Jueces");
            }

            var esCuentaLocal = juez.ProveedorAuth == "Local";
            var usuario = model.Usuario?.Trim();

            if (esCuentaLocal)
            {
                if (string.IsNullOrWhiteSpace(usuario))
                {
                    ModelState.AddModelError(nameof(model.Usuario), "Ingresa un usuario.");
                }
                else if (_context.Jueces.IgnoreQueryFilters().Any(j => j.Id != juez.Id && j.Usuario == usuario))
                {
                    ModelState.AddModelError(nameof(model.Usuario), "Ese usuario ya existe.");
                }
            }

            if (model.Rol != "Admin" && model.Rol != "Juez") model.Rol = "Juez";

            // Mismo criterio que "no puedes desactivar tu propia cuenta": un admin no puede dejarse
            // sin rol Admin, porque perderia el acceso a esta pantalla para deshacerlo.
            var esPropiaCuenta = juez.Id == HttpContext.Session.GetInt32("JuezId");
            if (esPropiaCuenta && model.Rol != "Admin")
            {
                ModelState.AddModelError(nameof(model.Rol), "No puedes quitarte a ti mismo el rol de administrador.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Nav = ObtenerNavContext("Jueces");
                var vm = ConstruirEditarJuezViewModel(juez);
                vm.NombreCompleto = model.NombreCompleto;
                vm.Usuario = model.Usuario;
                vm.Rol = model.Rol;
                return View(vm);
            }

            juez.NombreCompleto = model.NombreCompleto.Trim();
            if (esCuentaLocal) juez.Usuario = usuario;
            juez.Rol = model.Rol;

            var cambioPassword = esCuentaLocal && !string.IsNullOrEmpty(model.NuevaPassword);
            if (cambioPassword)
            {
                juez.Password = AccesoController.HashPassword(model.NuevaPassword!);
                // Una contrasena fijada por un tercero es de un solo uso (mismo trato que el admin semilla).
                // Si el admin se la cambia a si mismo, ya esta en sesion y la eligio el: no se le fuerza.
                juez.DebeCambiarPassword = !esPropiaCuenta;
            }

            _context.SaveChanges();

            _logger.LogInformation(
                "Juez editado por administracion. JuezEditadoId: {JuezEditadoId}, Rol: {Rol}, CambioPassword: {CambioPassword}, AdminId: {AdminId}",
                juez.Id, juez.Rol, cambioPassword, HttpContext.Session.GetInt32("JuezId"));

            // Si el admin se edito a si mismo, la sidebar lee el nombre de la sesion.
            if (esPropiaCuenta) HttpContext.Session.SetString("JuezNombre", juez.NombreCompleto);

            TempData["Mensaje"] = cambioPassword && !esPropiaCuenta
                ? $"'{juez.NombreCompleto}' actualizado. La nueva contraseña deberá cambiarse en su próximo inicio de sesión."
                : $"'{juez.NombreCompleto}' actualizado.";
            return RedirectToAction("Jueces");
        }

        private EditarJuezViewModel ConstruirEditarJuezViewModel(Juez juez) => new()
        {
            Id = juez.Id,
            NombreCompleto = juez.NombreCompleto,
            Usuario = juez.Usuario,
            Rol = juez.Rol,
            ProveedorAuth = juez.ProveedorAuth,
            CorreoInstitucional = juez.CorreoInstitucional,
            EsLaPropiaCuenta = juez.Id == HttpContext.Session.GetInt32("JuezId"),
            Pendiente = juez.Pendiente,
            Inactivo = juez.IsDeleted
        };

        // --- Registrantes y Equipos ---

        public async Task<IActionResult> RegistrantesYEquipos(string tab = "registrantes")
        {
            if (!EsAdmin()) return RedirigirSinPermisos();
            ViewBag.Nav = ObtenerNavContext("RegistrantesYEquipos");
            ViewBag.Tab = tab;

            if (tab == "equipos")
            {
                var periodo = PeriodoHelper.ObtenerPeriodoActual();
                // Se materializan las entidades y la proyeccion se hace en memoria a proposito: el
                // estado de la ficha tiene que salir de EquipoValidacionHelper y no de una copia de la
                // regla traducida a SQL. La copia anterior divergia de la canonica (ignoraba
                // Difunto.Nombre), asi que el panel del admin y el portal del alumno podian mostrar
                // estados contrarios para el mismo equipo. Son decenas de equipos por periodo, no miles.
                var equipos = await _context.EquiposConcurso
                    .Include(e => e.Carrera)
                    .Include(e => e.MaestroEncargado)
                    .Include(e => e.Difunto)
                    .Include(e => e.Integrantes)
                    .Include(e => e.Evaluacion)
                    .Where(e => e.Periodo == periodo)
                    .OrderBy(e => e.Nombre)
                    .ToListAsync();

                var tarjetas = equipos.Select(e => new EquipoAdminCardViewModel
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
                    FichaCompleta = EquipoValidacionHelper.EsFichaCompleta(e),
                    FichaFaltantes = EquipoValidacionHelper.FaltantesFicha(e),
                    FichaValidada = EquipoValidacionHelper.FueValidadaPorAdmin(e),
                    FichaValidadaEn = e.FichaValidadaPorAdminEn
                }).ToList();

                return View("RegistrantesYEquiposEquipos", tarjetas);
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

        // --- Edicion de registrantes ---
        //
        // Sin contrasena: es la cuenta del participante. IgnoreQueryFilters en todo el flujo para
        // que un registrante desactivado tambien se pueda corregir sin tener que reactivarlo antes.
        public async Task<IActionResult> EditarRegistrante(int id)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var registrante = await _context.Registrantes.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id);
            if (registrante == null)
            {
                TempData["Error"] = "El registrante ya no existe.";
                return RedirectToAction("RegistrantesYEquipos", new { tab = "registrantes" });
            }

            ViewBag.Nav = ObtenerNavContext("RegistrantesYEquipos");
            var vm = new EditarRegistranteViewModel
            {
                Id = registrante.Id,
                NombreCompleto = registrante.NombreCompleto,
                Tipo = registrante.Tipo,
                Identificador = registrante.Identificador,
                CorreoInstitucional = registrante.CorreoInstitucional,
                Telefono = registrante.Telefono,
                CatalogoGeneroId = registrante.CatalogoGeneroId,
                CatalogoCarreraId = registrante.CatalogoCarreraId,
                AutodescripcionCultural = registrante.AutodescripcionCultural
            };
            return View(await CompletarEditarRegistranteViewModelAsync(vm, registrante));
        }

        [HttpPost]
        public async Task<IActionResult> EditarRegistrante(EditarRegistranteViewModel model)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var registrante = await _context.Registrantes.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == model.Id);
            if (registrante == null)
            {
                TempData["Error"] = "El registrante ya no existe.";
                return RedirectToAction("RegistrantesYEquipos", new { tab = "registrantes" });
            }

            var identificador = model.Identificador.Trim();
            var correo = model.CorreoInstitucional.Trim().ToLowerInvariant();

            // Misma regla de dominio que el signup (7/5 digitos segun tipo, dominio @uabc.edu.mx),
            // evaluada sobre una copia para no tocar la entidad rastreada si falla.
            var borrador = new Registrante { Tipo = model.Tipo, Identificador = identificador, CorreoInstitucional = correo };
            foreach (var error in borrador.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(borrador)))
            {
                foreach (var member in error.MemberNames)
                {
                    ModelState.AddModelError(member, error.ErrorMessage ?? "Dato inválido.");
                }
            }

            // Unicidad contra TODOS los registrantes (tambien inactivos): si un inactivo ya usa el
            // identificador, reactivarlo despues chocaria.
            if (await _context.Registrantes.IgnoreQueryFilters().AnyAsync(r => r.Id != registrante.Id && r.Identificador == identificador))
            {
                ModelState.AddModelError(nameof(model.Identificador), "Ya existe otro registrante con ese identificador.");
            }
            if (await _context.Registrantes.IgnoreQueryFilters().AnyAsync(r => r.Id != registrante.Id && r.CorreoInstitucional.ToLower() == correo))
            {
                ModelState.AddModelError(nameof(model.CorreoInstitucional), "Ya existe otro registrante con ese correo.");
            }

            // Un alumno no puede ser maestro encargado: si este registrante ya lo es de algun equipo,
            // cambiarlo a Alumno dejaria esos equipos con un encargado invalido.
            if (model.Tipo == TipoRegistrante.Alumno
                && await _context.EquiposConcurso.IgnoreQueryFilters().AnyAsync(e => e.MaestroEncargadoId == registrante.Id))
            {
                ModelState.AddModelError(nameof(model.Tipo), "Este registrante es maestro encargado de un equipo; no puede cambiarse a Alumno.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Nav = ObtenerNavContext("RegistrantesYEquipos");
                return View(await CompletarEditarRegistranteViewModelAsync(model, registrante));
            }

            registrante.NombreCompleto = model.NombreCompleto.Trim();
            registrante.Tipo = model.Tipo;
            registrante.Identificador = identificador;
            registrante.CorreoInstitucional = correo;
            registrante.Telefono = string.IsNullOrWhiteSpace(model.Telefono) ? null : model.Telefono.Trim();
            registrante.CatalogoGeneroId = model.CatalogoGeneroId;
            registrante.CatalogoCarreraId = model.CatalogoCarreraId;
            registrante.AutodescripcionCultural = string.IsNullOrWhiteSpace(model.AutodescripcionCultural) ? null : model.AutodescripcionCultural.Trim();

            // Misma regla que el signup: si algun equipo esperaba a este identificador como maestro
            // encargado, se vincula ahora y se promueve a Maestro.
            var vinculados = 0;
            if (registrante.Tipo != TipoRegistrante.Alumno)
            {
                var equiposPendientes = await _context.EquiposConcurso
                    .Where(e => e.MaestroEncargadoIdentificadorPendiente == identificador)
                    .ToListAsync();
                foreach (var equipo in equiposPendientes)
                {
                    equipo.MaestroEncargadoId = registrante.Id;
                    equipo.MaestroEncargadoIdentificadorPendiente = null;
                }
                if (equiposPendientes.Count > 0) registrante.Tipo = TipoRegistrante.Maestro;
                vinculados = equiposPendientes.Count;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Registrante editado por administracion. RegistranteId: {RegistranteId}, AdminId: {AdminId}, EquiposVinculados: {Vinculados}",
                registrante.Id, HttpContext.Session.GetInt32("JuezId"), vinculados);

            TempData["Mensaje"] = vinculados > 0
                ? $"'{registrante.NombreCompleto}' actualizado y vinculado como maestro encargado de {vinculados} equipo(s) que lo esperaban."
                : $"'{registrante.NombreCompleto}' actualizado.";
            return RedirectToAction("RegistrantesYEquipos", new { tab = "registrantes" });
        }

        private async Task<EditarRegistranteViewModel> CompletarEditarRegistranteViewModelAsync(EditarRegistranteViewModel vm, Registrante registrante)
        {
            vm.ProveedorAuth = registrante.ProveedorAuth;
            vm.Activo = registrante.Activo;
            vm.CreadoEn = registrante.CreadoEn;
            var periodoActual = PeriodoHelper.ObtenerPeriodoActual();
            vm.EquipoActual = await _context.RegistranteEquipos
                .IgnoreQueryFilters()
                .Where(re => re.RegistranteId == registrante.Id && re.Periodo == periodoActual && re.Equipo.Activo)
                .Select(re => re.Equipo.Nombre)
                .FirstOrDefaultAsync();
            vm.Generos = await _context.CatalogoGeneros.Where(g => g.Activo).OrderBy(g => g.Orden).ToListAsync();
            vm.Carreras = await _context.CatalogoCarreras.Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync();
            return vm;
        }

        // --- Edicion de equipos (Ficha de Registro completa, vision.md 6.2) ---
        //
        // El admin NO esta sujeto a las fechas limite (current_state.md: FechaLimiteRequisitos bloquea
        // al participante, no al administrador). Con evaluacion registrada puede corregir los datos
        // de la ficha (el snapshot de la evaluacion no se reescribe), pero la lista de integrantes y
        // el narrador quedan cerrados: EvaluacionIntegrante y las constancias salen de esa lista.
        public async Task<IActionResult> EditarEquipo(int id)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var equipo = await ObtenerEquipoParaEdicionAsync(id);
            if (equipo == null)
            {
                TempData["Error"] = "El equipo ya no existe.";
                return RedirectToAction("RegistrantesYEquipos", new { tab = "equipos" });
            }

            ViewBag.Nav = ObtenerNavContext("RegistrantesYEquipos");
            var vm = new EditarEquipoViewModel
            {
                Id = equipo.Id,
                Nombre = equipo.Nombre,
                NombreAltar = equipo.NombreAltar,
                DifuntoNombre = equipo.Difunto?.Nombre ?? string.Empty,
                DifuntoFechaDefuncion = equipo.Difunto?.FechaDefuncion ?? DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
                CarreraId = equipo.CarreraId,
                MaestroEncargadoIdentificador = equipo.MaestroEncargado?.Identificador ?? equipo.MaestroEncargadoIdentificadorPendiente ?? string.Empty,
                UbicacionAltar = equipo.UbicacionAltar
            };
            return View(await CompletarEditarEquipoViewModelAsync(vm, equipo));
        }

        [HttpPost]
        public async Task<IActionResult> EditarEquipo(EditarEquipoViewModel model)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var equipo = await ObtenerEquipoParaEdicionAsync(model.Id);
            if (equipo == null)
            {
                TempData["Error"] = "El equipo ya no existe.";
                return RedirectToAction("RegistrantesYEquipos", new { tab = "equipos" });
            }

            var nombre = model.Nombre.Trim();
            if (await _context.EquiposConcurso.AnyAsync(e => e.Id != equipo.Id && e.Nombre == nombre && e.Periodo == equipo.Periodo))
            {
                ModelState.AddModelError(nameof(model.Nombre), "Ya existe un equipo con ese nombre en este periodo.");
            }

            if (!await _context.CatalogoCarreras.AnyAsync(c => c.Id == model.CarreraId && c.Activo))
            {
                ModelState.AddModelError(nameof(model.CarreraId), "Selecciona una carrera válida.");
            }

            var (maestroId, maestroPendiente, maestroError) = await MaestroEncargadoHelper.ResolverAsync(_context, model.MaestroEncargadoIdentificador);
            if (maestroError != null)
            {
                ModelState.AddModelError(nameof(model.MaestroEncargadoIdentificador), maestroError);
            }

            // Misma regla de "minimo un anio de fallecido" que aplica al participante, si el periodo la exige.
            var config = await _context.ConfiguracionesPeriodo.FirstOrDefaultAsync(c => c.Periodo == equipo.Periodo);
            if (config?.ExigirMinimoUnAnioFallecimiento == true
                && model.DifuntoFechaDefuncion > DateOnly.FromDateTime(DateTime.Today.AddYears(-1)))
            {
                ModelState.AddModelError(nameof(model.DifuntoFechaDefuncion), "La fecha de defunción debe ser de al menos 1 año atrás.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Nav = ObtenerNavContext("RegistrantesYEquipos");
                return View(await CompletarEditarEquipoViewModelAsync(model, equipo));
            }

            equipo.Nombre = nombre;
            equipo.NombreAltar = model.NombreAltar.Trim();
            equipo.CarreraId = model.CarreraId;
            equipo.MaestroEncargadoId = maestroId;
            equipo.MaestroEncargadoIdentificadorPendiente = maestroPendiente;
            equipo.UbicacionAltar = string.IsNullOrWhiteSpace(model.UbicacionAltar) ? null : model.UbicacionAltar.Trim();

            if (equipo.Difunto != null)
            {
                equipo.Difunto.Nombre = model.DifuntoNombre.Trim();
                equipo.Difunto.FechaDefuncion = model.DifuntoFechaDefuncion;
            }
            else
            {
                // Un equipo creado desde el portal siempre trae Difunto; si falta (datos historicos o
                // importados) el admin lo esta capturando aqui por primera vez.
                equipo.Difunto = new Difunto
                {
                    EquipoId = equipo.Id,
                    Nombre = model.DifuntoNombre.Trim(),
                    FechaDefuncion = model.DifuntoFechaDefuncion
                };
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Equipo editado por administracion. EquipoId: {EquipoId}, AdminId: {AdminId}, TeniaEvaluacion: {TeniaEvaluacion}",
                equipo.Id, HttpContext.Session.GetInt32("JuezId"), equipo.Evaluacion != null);

            TempData["Mensaje"] = equipo.Evaluacion != null
                ? $"Ficha de '{equipo.Nombre}' actualizada. La evaluación ya registrada conserva su snapshot original."
                : $"Ficha de '{equipo.Nombre}' actualizada.";
            return RedirectToAction("EditarEquipo", new { id = equipo.Id });
        }

        [HttpPost]
        public async Task<IActionResult> AgregarIntegranteEquipo(int equipoId, string identificador)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var equipo = await ObtenerEquipoParaEdicionAsync(equipoId);
            var bloqueo = BloqueoIntegrantesAdmin(equipo);
            if (bloqueo != null)
            {
                TempData["Error"] = bloqueo;
                return RedirigirTrasIntegrantes(equipoId, equipo);
            }

            var candidato = await _context.Registrantes.FirstOrDefaultAsync(r => r.Identificador == (identificador ?? string.Empty).Trim());
            if (candidato == null)
            {
                TempData["Error"] = "No existe un registrante activo con ese identificador.";
                return RedirectToAction("EditarEquipo", new { id = equipoId });
            }

            if (await _context.RegistranteEquipos.AnyAsync(re => re.RegistranteId == candidato.Id && re.Equipo.Periodo == equipo!.Periodo))
            {
                TempData["Error"] = $"{candidato.NombreCompleto} ya pertenece a un equipo en el periodo {equipo!.Periodo}.";
                return RedirectToAction("EditarEquipo", new { id = equipoId });
            }

            _context.RegistranteEquipos.Add(new RegistranteEquipo
            {
                RegistranteId = candidato.Id,
                EquipoId = equipo!.Id,
                Rol = RolEquipo.Integrante,
                Periodo = equipo.Periodo
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // SEC-11: el indice unico (RegistranteId, Periodo) gano la carrera.
                TempData["Error"] = $"{candidato.NombreCompleto} ya pertenece a un equipo en el periodo {equipo.Periodo}.";
                return RedirectToAction("EditarEquipo", new { id = equipoId });
            }

            TempData["Mensaje"] = $"{candidato.NombreCompleto} fue agregado al equipo.";
            return RedirectToAction("EditarEquipo", new { id = equipoId });
        }

        [HttpPost]
        public async Task<IActionResult> QuitarIntegranteEquipo(int equipoId, int registranteId)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var equipo = await ObtenerEquipoParaEdicionAsync(equipoId);
            var bloqueo = BloqueoIntegrantesAdmin(equipo);
            if (bloqueo != null)
            {
                TempData["Error"] = bloqueo;
                return RedirigirTrasIntegrantes(equipoId, equipo);
            }

            // El organizador es quien administra el equipo desde el portal; quitarlo dejaria al
            // equipo sin nadie que pueda editarlo desde ahi (vision.md 4.2: baja del organizador).
            if (registranteId == equipo!.CreadoPorRegistranteId)
            {
                TempData["Error"] = "El organizador del equipo no puede quitarse; reasigna el equipo antes.";
                return RedirectToAction("EditarEquipo", new { id = equipoId });
            }

            var vinculo = equipo.Integrantes.FirstOrDefault(i => i.RegistranteId == registranteId);
            if (vinculo != null)
            {
                _context.RegistranteEquipos.Remove(vinculo);
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = $"{vinculo.Registrante.NombreCompleto} fue quitado del equipo.";
            }

            return RedirectToAction("EditarEquipo", new { id = equipoId });
        }

        [HttpPost]
        public async Task<IActionResult> DesignarNarradorEquipo(int equipoId, int registranteId)
        {
            if (!EsAdmin()) return RedirigirSinPermisos();

            var equipo = await ObtenerEquipoParaEdicionAsync(equipoId);
            var bloqueo = BloqueoIntegrantesAdmin(equipo);
            if (bloqueo != null)
            {
                TempData["Error"] = bloqueo;
                return RedirigirTrasIntegrantes(equipoId, equipo);
            }

            if (equipo!.Integrantes.All(i => i.RegistranteId != registranteId))
            {
                TempData["Error"] = "Ese registrante no es integrante del equipo.";
                return RedirectToAction("EditarEquipo", new { id = equipoId });
            }

            foreach (var integrante in equipo.Integrantes)
            {
                integrante.Rol = integrante.RegistranteId == registranteId ? RolEquipo.Narrador : RolEquipo.Integrante;
            }

            await _context.SaveChangesAsync();
            TempData["Mensaje"] = "Narrador designado.";
            return RedirectToAction("EditarEquipo", new { id = equipoId });
        }

        private async Task<Equipo?> ObtenerEquipoParaEdicionAsync(int id)
        {
            return await _context.EquiposConcurso
                .Include(e => e.Carrera)
                .Include(e => e.Difunto)
                .Include(e => e.Evaluacion)
                .Include(e => e.MaestroEncargado)
                .Include(e => e.CreadoPorRegistrante)
                .Include(e => e.Integrantes).ThenInclude(ri => ri.Registrante)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        // Unico candado del admin sobre integrantes: consistencia con lo ya calificado.
        private static string? BloqueoIntegrantesAdmin(Equipo? equipo)
        {
            if (equipo == null) return "El equipo ya no existe.";
            if (equipo.Evaluacion != null)
            {
                return "El equipo ya tiene una calificación registrada; la lista de integrantes quedó cerrada " +
                       "para que coincida con lo que el juez evaluó.";
            }
            return null;
        }

        private IActionResult RedirigirTrasIntegrantes(int equipoId, Equipo? equipo) =>
            equipo == null
                ? RedirectToAction("RegistrantesYEquipos", new { tab = "equipos" })
                : RedirectToAction("EditarEquipo", new { id = equipoId });

        private async Task<EditarEquipoViewModel> CompletarEditarEquipoViewModelAsync(EditarEquipoViewModel vm, Equipo equipo)
        {
            vm.Periodo = equipo.Periodo;
            vm.CreadoEn = equipo.CreadoEn;
            vm.OrganizadorNombre = equipo.CreadoPorRegistrante?.NombreCompleto;
            vm.CreadoPorRegistranteId = equipo.CreadoPorRegistranteId;
            vm.MaestroEncargadoNombre = equipo.MaestroEncargado?.NombreCompleto;
            vm.MaestroEncargadoPendiente = equipo.MaestroEncargadoId == null && !string.IsNullOrEmpty(equipo.MaestroEncargadoIdentificadorPendiente);
            vm.TieneEvaluacion = equipo.Evaluacion != null;
            vm.EstadoEvaluacion = equipo.Evaluacion?.Estado.ToString();
            vm.FichaValidada = EquipoValidacionHelper.FueValidadaPorAdmin(equipo);
            vm.FichaValidadaEn = equipo.FichaValidadaPorAdminEn;
            vm.FichaFaltantes = EquipoValidacionHelper.FaltantesFicha(equipo);
            vm.TieneNarrador = EquipoValidacionHelper.TieneNarradorDesignado(equipo);
            vm.Carreras = await _context.CatalogoCarreras.Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync();
            vm.Integrantes = equipo.Integrantes
                .OrderByDescending(i => i.RegistranteId == equipo.CreadoPorRegistranteId)
                .ThenBy(i => i.Registrante.NombreCompleto)
                .ToList();
            return vm;
        }

        // --- Validacion administrativa de la Ficha de Registro ---
        //
        // Marca la ficha de un equipo como revisada por administracion para que deje de listarse como
        // pendiente. Es un estado de SEGUIMIENTO: no rellena datos, no relaja EsFichaCompleta y no
        // habilita el cierre como Final (eso lo sigue decidiendo EquipoValidacionHelper con los datos
        // reales, porque el snapshot de la evaluacion y las constancias los necesitan).
        //
        // Se llama desde dos lugares: el listado admin de Equipos (POST normal) y Nueva Evaluacion
        // (fetch, para no perder una evaluacion a medio capturar). De ahi la doble respuesta.
        [HttpPost]
        public async Task<IActionResult> ValidarFicha(int equipoId, string? origen)
        {
            if (!EsAdmin()) return RespuestaSinPermisosFicha();
            return await CambiarValidacionFichaAsync(equipoId, validar: true, origen);
        }

        [HttpPost]
        public async Task<IActionResult> QuitarValidacionFicha(int equipoId, string? origen)
        {
            if (!EsAdmin()) return RespuestaSinPermisosFicha();
            return await CambiarValidacionFichaAsync(equipoId, validar: false, origen);
        }

        private async Task<IActionResult> CambiarValidacionFichaAsync(int equipoId, bool validar, string? origen)
        {
            // El cliente solo manda el id: el estado administrativo se lee y se escribe server-side
            // sobre la entidad recargada, nunca se acepta posteado.
            var equipo = await _context.EquiposConcurso
                .Include(e => e.Difunto)
                .Include(e => e.Integrantes)
                .FirstOrDefaultAsync(e => e.Id == equipoId);

            if (equipo == null)
            {
                if (EsPeticionFetch()) return NotFound();
                TempData["Error"] = "El equipo ya no existe.";
                return RedirigirTrasValidacion(equipoId, origen);
            }

            var juezId = HttpContext.Session.GetInt32("JuezId");
            equipo.FichaValidadaPorAdminEn = validar ? FechaConcursoHelper.AhoraLocal : null;
            equipo.FichaValidadaPorJuezId = validar ? juezId : null;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Ficha de registro {Accion} por administracion. EquipoId: {EquipoId}, JuezId: {JuezId}",
                validar ? "validada" : "desmarcada", equipo.Id, juezId);

            var faltantes = EquipoValidacionHelper.FaltantesFicha(equipo);
            var mensaje = ConstruirMensajeValidacion(equipo.Nombre, validar, faltantes);

            if (EsPeticionFetch())
            {
                return Json(new
                {
                    validada = validar,
                    completa = faltantes.Count == 0,
                    faltantes,
                    mensaje
                });
            }

            TempData[validar && faltantes.Count > 0 ? "Error" : "Mensaje"] = mensaje;
            return RedirigirTrasValidacion(equipo.Id, origen);
        }

        private static string ConstruirMensajeValidacion(string nombreEquipo, bool validar, IReadOnlyList<string> faltantes)
        {
            if (!validar) return $"La ficha de '{nombreEquipo}' vuelve a contar como pendiente.";

            if (faltantes.Count == 0) return $"Ficha de '{nombreEquipo}' validada por administración.";

            // Se permite validar una ficha incompleta (el admin puede haber confirmado los datos por
            // fuera), pero no se oculta que sigue faltando informacion para poder cerrar Final.
            return $"Ficha de '{nombreEquipo}' validada por administración, pero sigue faltando " +
                   $"{string.Join(", ", faltantes)}: la evaluación no se podrá cerrar como Final hasta completarlo.";
        }

        // El origen viene del cliente, asi que se resuelve contra una lista cerrada en vez de
        // redirigir a una URL posteada (open redirect).
        private IActionResult RedirigirTrasValidacion(int equipoId, string? origen) =>
            origen == "evaluacion"
                ? RedirectToAction("NuevaEvaluacion", "AltarEvaluacion", new { equipoId })
                : RedirectToAction("RegistrantesYEquipos", new { tab = "equipos" });

        private bool EsPeticionFetch() =>
            HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        private IActionResult RespuestaSinPermisosFicha()
        {
            if (EsPeticionFetch())
            {
                _logger.LogWarning(
                    "Acceso denegado (rol insuficiente) a validacion de ficha. JuezId: {JuezId}, Rol: {Rol}, IP: {IP}",
                    HttpContext.Session.GetInt32("JuezId"), HttpContext.Session.GetString("JuezRol"),
                    HttpContext.Connection.RemoteIpAddress);
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            return RedirigirSinPermisos();
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
                ModelState.AddModelError(string.Empty, "Los pesos de la nota final deben sumar 100%.");
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
