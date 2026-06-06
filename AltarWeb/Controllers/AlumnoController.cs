using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AltarWeb.Models;
using AltarWeb.Models.ViewModels;
using AltarWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Controllers
{
    public class AlumnoController : Controller
    {
        private readonly AltarDbContext _context;
        private readonly ConstanciaService _constanciaService;

        public AlumnoController(AltarDbContext context, ConstanciaService constanciaService)
        {
            _context = context;
            _constanciaService = constanciaService;
        }

        public IActionResult Login(string? error = null)
        {
            ViewBag.Error = error;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(AlumnoLoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var alumno = await _context.Alumnos
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Matricula == model.Matricula);

            if (alumno == null)
            {
                ModelState.AddModelError(string.Empty, "Matricula o contrasena incorrectos.");
                return View(model);
            }

            if (!alumno.Activo)
            {
                return RedirectToAction("Login", new { error = "Tu cuenta fue desactivada. Contacta a un administrador." });
            }

            if (alumno.ProveedorAuth == "Google")
            {
                return RedirectToAction("Login", new { error = "Esta cuenta usa Google. Inicia sesion con el boton de Google." });
            }

            if (!VerificarHash(model.Password, alumno.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Matricula o contrasena incorrectos.");
                return View(model);
            }

            CrearSesionAlumno(alumno);
            return RedirectToAction("Dashboard");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Remove("AlumnoId");
            HttpContext.Session.Remove("AlumnoNombre");
            HttpContext.Session.Remove("AlumnoMatricula");
            return RedirectToAction("Login");
        }

        public IActionResult Registro()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Registro(AlumnoRegistroViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var correo = model.CorreoElectronico.Trim().ToLowerInvariant();
            if (!correo.EndsWith("@uabc.edu.mx"))
            {
                ModelState.AddModelError(nameof(model.CorreoElectronico), "Debe ser un correo @uabc.edu.mx");
                return View(model);
            }

            if (await _context.Alumnos.IgnoreQueryFilters().AnyAsync(a => a.Matricula == model.Matricula))
            {
                ModelState.AddModelError(nameof(model.Matricula), "Esta matricula ya esta registrada.");
                return View(model);
            }

            if (await _context.Alumnos.IgnoreQueryFilters().AnyAsync(a => a.CorreoElectronico == correo))
            {
                ModelState.AddModelError(nameof(model.CorreoElectronico), "Este correo ya esta registrado.");
                return View(model);
            }

            var alumno = new Alumno
            {
                NombreCompleto = model.NombreCompleto.Trim(),
                Matricula = model.Matricula.Trim(),
                CorreoElectronico = correo,
                PasswordHash = HashPassword(model.Password),
                ProveedorAuth = "Local",
                PeriodoRegistro = PeriodoHelper.ObtenerPeriodoActual()
            };

            _context.Alumnos.Add(alumno);
            await _context.SaveChangesAsync();

            CrearSesionAlumno(alumno);
            return RedirectToAction("Dashboard");
        }

        public IActionResult LoginGoogle()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleCallback", "Alumno")
            };
            return Challenge(properties, "Google");
        }

        public async Task<IActionResult> GoogleCallback()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded || result.Principal == null)
            {
                return RedirectToAction("Login", new { error = "Error al autenticar con Google." });
            }

            var email = result.Principal.FindFirstValue(ClaimTypes.Email)?.ToLowerInvariant();
            var nombre = result.Principal.FindFirstValue(ClaimTypes.Name);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (string.IsNullOrEmpty(email) || !email.EndsWith("@uabc.edu.mx"))
            {
                return RedirectToAction("Login", new { error = "Solo se permiten cuentas institucionales @uabc.edu.mx." });
            }

            var alumno = await _context.Alumnos
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.CorreoElectronico == email);

            if (alumno != null)
            {
                if (!alumno.Activo)
                {
                    return RedirectToAction("Login", new { error = "Tu cuenta fue desactivada. Contacta a un administrador." });
                }

                CrearSesionAlumno(alumno);
                return RedirectToAction("Dashboard");
            }

            HttpContext.Session.SetString("GoogleEmail", email);
            HttpContext.Session.SetString("GoogleNombre", nombre ?? string.Empty);
            return RedirectToAction("CompletarRegistroGoogle");
        }

        public IActionResult CompletarRegistroGoogle()
        {
            var email = HttpContext.Session.GetString("GoogleEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login");

            return View(new CompletarRegistroGoogleViewModel
            {
                CorreoElectronico = email,
                NombreCompleto = HttpContext.Session.GetString("GoogleNombre") ?? string.Empty
            });
        }

        [HttpPost]
        public async Task<IActionResult> CompletarRegistroGoogle(CompletarRegistroGoogleViewModel model)
        {
            var email = HttpContext.Session.GetString("GoogleEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login");
            model.CorreoElectronico = email;
            model.NombreCompleto = HttpContext.Session.GetString("GoogleNombre") ?? model.NombreCompleto;

            if (!ModelState.IsValid) return View(model);

            if (await _context.Alumnos.IgnoreQueryFilters().AnyAsync(a => a.Matricula == model.Matricula))
            {
                ModelState.AddModelError(nameof(model.Matricula), "Esta matricula ya esta registrada.");
                return View(model);
            }

            var alumno = new Alumno
            {
                NombreCompleto = model.NombreCompleto.Trim(),
                Matricula = model.Matricula.Trim(),
                CorreoElectronico = email,
                PasswordHash = null,
                ProveedorAuth = "Google",
                PeriodoRegistro = PeriodoHelper.ObtenerPeriodoActual()
            };

            _context.Alumnos.Add(alumno);
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("GoogleEmail");
            HttpContext.Session.Remove("GoogleNombre");

            CrearSesionAlumno(alumno);
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Dashboard()
        {
            var alumno = ObtenerAlumnoSesion();
            if (alumno == null) return RedirectToAction("Login");

            var periodoActual = PeriodoHelper.ObtenerPeriodoActual();
            var equipo = await ObtenerEquipoAlumnoAsync(alumno.Id, periodoActual);
            Evaluacion? evaluacion = null;
            if (equipo != null)
            {
                evaluacion = await _context.Evaluaciones.FirstOrDefaultAsync(ev => ev.EquipoId == equipo.Id);
            }

            return View(new AlumnoDashboardViewModel
            {
                Alumno = alumno,
                Equipo = equipo,
                Evaluacion = evaluacion,
                EsCreador = equipo != null && equipo.CreadoPorAlumnoId == alumno.Id
            });
        }

        public IActionResult CrearEquipo()
        {
            var alumno = ObtenerAlumnoSesion();
            if (alumno == null) return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CrearEquipo(CrearEquipoViewModel model)
        {
            var alumno = ObtenerAlumnoSesion();
            if (alumno == null) return RedirectToAction("Login");
            if (!ModelState.IsValid) return View(model);

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var tieneEquipo = await _context.AlumnoEquipos
                .AnyAsync(ae => ae.AlumnoId == alumno.Id && ae.Equipo.PeriodoAcademico == periodo);

            if (tieneEquipo)
            {
                ModelState.AddModelError(string.Empty, "Ya perteneces a un equipo este semestre.");
                return View(model);
            }

            var nombre = model.NombreEquipo.Trim();
            var nombreDuplicado = await _context.Equipos
                .AnyAsync(e => e.NombreEquipo == nombre && e.PeriodoAcademico == periodo);
            if (nombreDuplicado)
            {
                ModelState.AddModelError(nameof(model.NombreEquipo), "Ya existe un equipo con ese nombre en este semestre.");
                return View(model);
            }

            var idsIntegrantes = (model.IntegranteIds ?? new List<int>()).Distinct().ToList();
            var alumnosConEquipo = await _context.AlumnoEquipos
                .Where(ae => idsIntegrantes.Contains(ae.AlumnoId) && ae.Equipo.PeriodoAcademico == periodo)
                .Select(ae => ae.AlumnoId)
                .ToListAsync();

            if (alumnosConEquipo.Count != 0)
            {
                ModelState.AddModelError(string.Empty, "Uno o mas integrantes ya pertenecen a un equipo este semestre.");
                return View(model);
            }

            var equipo = new Equipo
            {
                NombreEquipo = nombre,
                PeriodoAcademico = periodo,
                CreadoPorAlumnoId = alumno.Id,
                SnapshotNombreCreador = alumno.NombreCompleto
            };

            _context.Equipos.Add(equipo);
            await _context.SaveChangesAsync();

            _context.AlumnoEquipos.Add(new AlumnoEquipo { AlumnoId = alumno.Id, EquipoId = equipo.Id, EsCreador = true });
            foreach (var integranteId in idsIntegrantes.Where(id => id != alumno.Id))
            {
                _context.AlumnoEquipos.Add(new AlumnoEquipo { AlumnoId = integranteId, EquipoId = equipo.Id });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("MiEquipo");
        }

        public async Task<IActionResult> MiEquipo()
        {
            var alumno = ObtenerAlumnoSesion();
            if (alumno == null) return RedirectToAction("Login");

            var equipo = await ObtenerEquipoAlumnoAsync(alumno.Id, PeriodoHelper.ObtenerPeriodoActual());
            if (equipo == null) return RedirectToAction("CrearEquipo");

            var equipoEvaluado = await _context.Evaluaciones.AnyAsync(ev => ev.EquipoId == equipo.Id);
            ViewBag.EsCreador = equipo.CreadoPorAlumnoId == alumno.Id;
            ViewBag.EquipoEvaluado = equipoEvaluado;
            ViewBag.PuedeEditarIntegrantes = equipo.CreadoPorAlumnoId == alumno.Id && !equipoEvaluado;

            return View(equipo);
        }

        [HttpPost]
        public async Task<IActionResult> AgregarIntegrante(int integranteId)
        {
            var alumno = ObtenerAlumnoSesion();
            if (alumno == null) return Json(new { ok = false, error = "Sin sesion." });

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var equipo = await _context.AlumnoEquipos
                .Where(ae => ae.AlumnoId == alumno.Id && ae.EsCreador && ae.Equipo.PeriodoAcademico == periodo)
                .Select(ae => ae.Equipo)
                .FirstOrDefaultAsync();

            if (equipo == null) return Json(new { ok = false, error = "No eres el creador de ningun equipo activo." });

            var equipoEvaluado = await _context.Evaluaciones.AnyAsync(ev => ev.EquipoId == equipo.Id);
            if (equipoEvaluado) return Json(new { ok = false, error = "El equipo ya fue evaluado y no puede cambiar integrantes." });

            var alumnoExiste = await _context.Alumnos.AnyAsync(a => a.Id == integranteId);
            if (!alumnoExiste) return Json(new { ok = false, error = "Alumno no encontrado." });

            var yaEstaEnEquipo = await _context.AlumnoEquipos
                .AnyAsync(ae => ae.AlumnoId == integranteId && ae.Equipo.PeriodoAcademico == periodo);
            if (yaEstaEnEquipo) return Json(new { ok = false, error = "Este alumno ya pertenece a un equipo este semestre." });

            _context.AlumnoEquipos.Add(new AlumnoEquipo { AlumnoId = integranteId, EquipoId = equipo.Id });
            await _context.SaveChangesAsync();
            return Json(new { ok = true });
        }

        [HttpPost]
        public async Task<IActionResult> QuitarIntegrante(int integranteId)
        {
            var alumno = ObtenerAlumnoSesion();
            if (alumno == null) return Json(new { ok = false, error = "Sin sesion." });

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var equipo = await _context.AlumnoEquipos
                .Where(ae => ae.AlumnoId == alumno.Id && ae.EsCreador && ae.Equipo.PeriodoAcademico == periodo)
                .Select(ae => ae.Equipo)
                .FirstOrDefaultAsync();

            if (equipo == null) return Json(new { ok = false, error = "No eres el creador de ningun equipo activo." });

            var equipoEvaluado = await _context.Evaluaciones.AnyAsync(ev => ev.EquipoId == equipo.Id);
            if (equipoEvaluado) return Json(new { ok = false, error = "El equipo ya fue evaluado y no puede cambiar integrantes." });

            if (integranteId == alumno.Id || integranteId == equipo.CreadoPorAlumnoId)
            {
                return Json(new { ok = false, error = "No puedes quitar al creador del equipo." });
            }

            var integrante = await _context.AlumnoEquipos
                .FirstOrDefaultAsync(ae => ae.EquipoId == equipo.Id && ae.AlumnoId == integranteId && !ae.EsCreador);

            if (integrante == null) return Json(new { ok = false, error = "El integrante no pertenece a tu equipo." });

            _context.AlumnoEquipos.Remove(integrante);
            await _context.SaveChangesAsync();
            return Json(new { ok = true });
        }

        public async Task<IActionResult> BuscarAlumno(string q)
        {
            var alumnoSesion = ObtenerAlumnoSesion();
            if (alumnoSesion == null || string.IsNullOrWhiteSpace(q)) return Json(new List<object>());

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var conEquipo = await _context.AlumnoEquipos
                .Where(ae => ae.Equipo.PeriodoAcademico == periodo)
                .Select(ae => ae.AlumnoId)
                .ToListAsync();

            var texto = q.ToLowerInvariant();
            var resultados = await _context.Alumnos
                .Where(a => a.Id != alumnoSesion.Id
                    && !conEquipo.Contains(a.Id)
                    && (a.Matricula.Contains(q) || a.NombreCompleto.ToLower().Contains(texto)))
                .Take(10)
                .Select(a => new { a.Id, a.NombreCompleto, a.Matricula })
                .ToListAsync();

            return Json(resultados);
        }

        public async Task<IActionResult> MiEvaluacion()
        {
            var alumno = ObtenerAlumnoSesion();
            if (alumno == null) return RedirectToAction("Login");

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var equipoId = await _context.AlumnoEquipos
                .Where(ae => ae.AlumnoId == alumno.Id && ae.Equipo.PeriodoAcademico == periodo)
                .Select(ae => ae.EquipoId)
                .FirstOrDefaultAsync();

            if (equipoId == 0)
            {
                return View(new MiEvaluacionViewModel { Estado = EstadoEvaluacion.SinEquipo });
            }

            var evaluacion = await _context.Evaluaciones
                .Include(ev => ev.Equipo)
                    .ThenInclude(e => e!.Integrantes)
                        .ThenInclude(ae => ae.Alumno)
                .FirstOrDefaultAsync(ev => ev.EquipoId == equipoId);

            if (evaluacion == null)
            {
                var nombreEquipo = await _context.Equipos.Where(e => e.Id == equipoId).Select(e => e.NombreEquipo).FirstOrDefaultAsync();
                return View(new MiEvaluacionViewModel
                {
                    Estado = EstadoEvaluacion.PendienteEvaluacion,
                    NombreEquipo = nombreEquipo ?? string.Empty
                });
            }

            return View(new MiEvaluacionViewModel
            {
                Estado = EstadoEvaluacion.Evaluado,
                NombreEquipo = evaluacion.ObtenerNombreEquipo(),
                NombreDifunto = evaluacion.NombreDifunto,
                TipoAltar = evaluacion.TipoAltar,
                Niveles = evaluacion.Niveles,
                ElementosPresentes = evaluacion.ObtenerConteoElementos(),
                BonusTematicos = evaluacion.BonusTematicos,
                NotaTradicionFinal = evaluacion.NotaTradicionFinal,
                NotaPersonalizacionFinal = evaluacion.NotaPersonalizacionFinal,
                NotaEstetica = evaluacion.NotaEstetica,
                NotaFinal = evaluacion.NotaFinal,
                EvaluacionId = evaluacion.Id,
                PuedeDescargarConstancia = evaluacion.NotaFinal >= 9.0m
            });
        }

        public async Task<IActionResult> DescargarMiConstancia(int evaluacionId)
        {
            var alumno = ObtenerAlumnoSesion();
            if (alumno == null) return RedirectToAction("Login");

            var evaluacion = await _context.Evaluaciones
                .Include(ev => ev.Equipo)
                    .ThenInclude(e => e!.Integrantes)
                .FirstOrDefaultAsync(ev => ev.Id == evaluacionId);

            if (evaluacion == null) return NotFound();
            if (evaluacion.NotaFinal < 9.0m) return Forbid();

            var esIntegrante = evaluacion.Equipo?.Integrantes.Any(ae => ae.AlumnoId == alumno.Id) ?? false;
            if (!esIntegrante) return Forbid();

            var pdf = _constanciaService.GenerarIndividual(evaluacion, alumno.NombreCompleto, evaluacion.ObtenerNombreEquipo());
            return File(pdf, "application/pdf", $"Constancia_{alumno.NombreCompleto}.pdf");
        }

        private void CrearSesionAlumno(Alumno alumno)
        {
            HttpContext.Session.SetInt32("AlumnoId", alumno.Id);
            HttpContext.Session.SetString("AlumnoNombre", alumno.NombreCompleto);
            HttpContext.Session.SetString("AlumnoMatricula", alumno.Matricula);
        }

        private Alumno? ObtenerAlumnoSesion()
        {
            var id = HttpContext.Session.GetInt32("AlumnoId");
            if (id == null) return null;
            return _context.Alumnos.Find(id.Value);
        }

        private async Task<Equipo?> ObtenerEquipoAlumnoAsync(int alumnoId, string periodo)
        {
            return await _context.AlumnoEquipos
                .Include(ae => ae.Equipo)
                    .ThenInclude(e => e.Integrantes)
                        .ThenInclude(ae => ae.Alumno)
                .Where(ae => ae.AlumnoId == alumnoId && ae.Equipo.PeriodoAcademico == periodo)
                .Select(ae => ae.Equipo)
                .FirstOrDefaultAsync();
        }

        private static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerificarHash(string password, string? hash)
        {
            return !string.IsNullOrEmpty(hash) && HashPassword(password) == hash;
        }
    }
}
