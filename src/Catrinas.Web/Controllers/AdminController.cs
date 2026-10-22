using System.Globalization;
using Catrinas.Web.Datos;
using Catrinas.Web.Modelos;
using Catrinas.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Web.Controllers
{
    // Administracion de Catrinas: jurado (alta, aprobacion, baja), participantes, inscripciones y
    // configuracion del periodo (fechas limite, pesos de los cinco criterios y umbral de privacidad).
    [SoloAdmin]
    public class AdminController : Controller
    {
        private readonly CatrinasDbContext _context;

        public AdminController(CatrinasDbContext context) => _context = context;

        public async Task<IActionResult> Jurado()
        {
            ViewBag.Nav = this.Nav("Jurado");
            return View(await _context.Jueces.AsNoTracking().OrderBy(j => !j.Pendiente).ThenBy(j => j.NombreCompleto).ToListAsync());
        }

        public IActionResult CrearJuez()
        {
            ViewBag.Nav = this.Nav("Jurado");
            return View(new CrearJuezViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> CrearJuez(CrearJuezViewModel model)
        {
            var usuario = (model.Usuario ?? string.Empty).Trim();
            if (await _context.Jueces.AnyAsync(j => j.Usuario == usuario)) ModelState.AddModelError(nameof(model.Usuario), "Ese usuario ya existe.");
            if (!ModelState.IsValid)
            {
                ViewBag.Nav = this.Nav("Jurado");
                return View(model);
            }

            _context.Jueces.Add(new Juez
            {
                Usuario = usuario,
                NombreCompleto = model.NombreCompleto.Trim(),
                Password = PasswordHashService.HashPassword(model.Password),
                Rol = model.EsAdmin ? RolesJuez.Admin : RolesJuez.Juez,
                DebeCambiarPassword = true
            });
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Ese usuario ya existe.";
                return RedirectToAction(nameof(Jurado));
            }
            TempData["Mensaje"] = "Cuenta creada. Deberá cambiar su contraseña al entrar.";
            return RedirectToAction(nameof(Jurado));
        }

        [HttpPost]
        public Task<IActionResult> AprobarJuez(int id) => CambiarJuezAsync(id, j => j.Pendiente = false, "Solicitud aprobada.");

        [HttpPost]
        public Task<IActionResult> DesactivarJuez(int id) => CambiarJuezAsync(id, j => { j.Activo = false; j.FechaBaja = DateTime.UtcNow; }, "Cuenta desactivada.");

        [HttpPost]
        public Task<IActionResult> ReactivarJuez(int id) => CambiarJuezAsync(id, j => { j.Activo = true; j.FechaBaja = null; }, "Cuenta reactivada.");

        private async Task<IActionResult> CambiarJuezAsync(int id, Action<Juez> cambio, string mensaje)
        {
            if (id == HttpContext.Session.JuezId())
            {
                TempData["Error"] = "No puedes modificar tu propia cuenta desde aquí.";
                return RedirectToAction(nameof(Jurado));
            }
            var juez = await _context.Jueces.FirstOrDefaultAsync(j => j.Id == id);
            if (juez == null) return NotFound();
            cambio(juez);
            await _context.SaveChangesAsync();
            TempData["Mensaje"] = mensaje;
            return RedirectToAction(nameof(Jurado));
        }

        // Gestion individual: muestra el genero (necesario para la gestion) pero NO la autodescripcion
        // cultural, que solo se usa agregada con el umbral de privacidad.
        public async Task<IActionResult> Participantes()
        {
            ViewBag.Nav = this.Nav("Participantes");
            return View(await _context.Registrantes.AsNoTracking()
                .Include(r => r.CatalogoCarrera).Include(r => r.CatalogoGenero)
                .OrderBy(r => r.NombreCompleto).ToListAsync());
        }

        [HttpPost]
        public Task<IActionResult> DesactivarParticipante(int id) => CambiarParticipanteAsync(id, false);

        [HttpPost]
        public Task<IActionResult> ReactivarParticipante(int id) => CambiarParticipanteAsync(id, true);

        private async Task<IActionResult> CambiarParticipanteAsync(int id, bool activo)
        {
            var registrante = await _context.Registrantes.FirstOrDefaultAsync(r => r.Id == id);
            if (registrante == null) return NotFound();
            registrante.Activo = activo;
            await _context.SaveChangesAsync();
            TempData["Mensaje"] = activo ? "Participante reactivado." : "Participante desactivado.";
            return RedirectToAction(nameof(Participantes));
        }

        // Gate R6 y prueba de carga (F4): como llega la peticion al hosting (IP, esquema, X-Forwarded-*) y
        // memoria del proceso. Solo administradores; sin cookies, tokens ni datos personales.
        public IActionResult Diagnostico() => Json(Concurso.Core.Diagnostico.DiagnosticoServidor.Construir(HttpContext));

        public async Task<IActionResult> Configuracion()
        {
            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var c = await ConfiguracionCatrinas.ObtenerAsync(_context, periodo);
            ViewBag.Nav = this.Nav("Configuracion");
            return View(new ConfiguracionViewModel
            {
                Periodo = periodo,
                FechaLimiteInscripcion = c.FechaLimiteInscripcion,
                FechaLimiteRequisitos = c.FechaLimiteRequisitos,
                PesoVestuarioPct = Porcentaje(c.PesoVestuario),
                PesoMaquillajePct = Porcentaje(c.PesoMaquillaje),
                PesoAccesoriosPct = Porcentaje(c.PesoAccesorios),
                PesoOriginalidadCreatividadPct = Porcentaje(c.PesoOriginalidadCreatividad),
                PesoApegoTradicionesPct = Porcentaje(c.PesoApegoTradiciones),
                UmbralAgrupacionDemografica = c.UmbralAgrupacionDemografica
            });
        }

        // B6 (2026-10-21): la columna es decimal(5,4), asi que 0.2500 * 100 se mostraba como "25.00". Se quita la
        // escala sobrante para mostrar "25", como Altares (la referencia de diseño).
        private static decimal Porcentaje(decimal fraccion) =>
            decimal.Parse((fraccion * 100).ToString("0.##", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        [HttpPost]
        public async Task<IActionResult> Configuracion(ConfiguracionViewModel model)
        {
            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            model.Periodo = periodo;
            if (model.SumaPesos != 100) ModelState.AddModelError(string.Empty, "Los pesos de los cinco criterios deben sumar 100%.");
            if (model.UmbralAgrupacionDemografica < PrivacidadReporteHelper.UmbralMinimo)
            {
                ModelState.AddModelError(nameof(model.UmbralAgrupacionDemografica), $"El umbral no puede ser menor que {PrivacidadReporteHelper.UmbralMinimo}.");
            }
            // M3 (pruebas adversariales 2026-10-21): se aceptaba el año 0009 (dejo la inscripcion cerrada en las
            // pruebas manuales) y una fecha de requisitos anterior a la de inscripcion.
            static bool FueraDeRango(DateTime? fecha) => fecha is { Year: < 2000 or > 2099 };
            if (FueraDeRango(model.FechaLimiteInscripcion))
            {
                ModelState.AddModelError(nameof(model.FechaLimiteInscripcion), "La fecha límite de inscripción debe estar entre los años 2000 y 2099.");
            }
            if (FueraDeRango(model.FechaLimiteRequisitos))
            {
                ModelState.AddModelError(nameof(model.FechaLimiteRequisitos), "La fecha límite de requisitos debe estar entre los años 2000 y 2099.");
            }
            if (model.FechaLimiteInscripcion != null && model.FechaLimiteRequisitos != null && model.FechaLimiteRequisitos < model.FechaLimiteInscripcion)
            {
                ModelState.AddModelError(nameof(model.FechaLimiteRequisitos), "La fecha límite de requisitos no puede ser anterior a la de inscripción.");
            }
            if (!ModelState.IsValid)
            {
                ViewBag.Nav = this.Nav("Configuracion");
                return View(model);
            }

            var config = await _context.Configuraciones.FirstOrDefaultAsync(c => c.Periodo == periodo);
            if (config == null)
            {
                config = new ConfiguracionPeriodo { Periodo = periodo };
                _context.Configuraciones.Add(config);
            }
            config.FechaLimiteInscripcion = model.FechaLimiteInscripcion;
            config.FechaLimiteRequisitos = model.FechaLimiteRequisitos;
            config.PesoVestuario = model.PesoVestuarioPct / 100;
            config.PesoMaquillaje = model.PesoMaquillajePct / 100;
            config.PesoAccesorios = model.PesoAccesoriosPct / 100;
            config.PesoOriginalidadCreatividad = model.PesoOriginalidadCreatividadPct / 100;
            config.PesoApegoTradiciones = model.PesoApegoTradicionesPct / 100;
            config.UmbralAgrupacionDemografica = model.UmbralAgrupacionDemografica;
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Configuración guardada. Los pesos se aplican a las evaluaciones que se guarden a partir de ahora; las ya guardadas no se recalculan.";
            return RedirectToAction(nameof(Configuracion));
        }
    }
}
