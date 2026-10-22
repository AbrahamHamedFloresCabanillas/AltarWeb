using Catrinas.Web.Datos;
using Catrinas.Web.Modelos;
using Catrinas.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Catrinas.Web.Controllers
{
    // Portal de participantes de Catrinas: acceso, registro local y con Google, solicitud de acceso al
    // jurado y panel del participante.
    public class RegistroController : Controller
    {
        private const string AmbitoLimite = "registrante";
        private readonly CatrinasDbContext _context;
        private readonly ILogger<RegistroController> _logger;
        private readonly LimitadorIntentosLogin _limitador;
        private readonly IOptions<ValidacionGoogleOptions> _validacionGoogle;

        public RegistroController(CatrinasDbContext context, ILogger<RegistroController> logger, LimitadorIntentosLogin limitador,
            IOptions<ValidacionGoogleOptions> validacionGoogle)
        {
            _context = context;
            _logger = logger;
            _limitador = limitador;
            _validacionGoogle = validacionGoogle;
        }

        [AccesoPublico]
        public IActionResult Login(string? error = null, string? tab = null)
        {
            ViewBag.Error = error;
            ViewBag.Tab = tab == "jurado" ? "jurado" : "participantes";
            return View(new LoginParticipanteViewModel());
        }

        [AccesoPublico]
        [HttpPost]
        [EnableRateLimiting(PoliticasRateLimit.LoginIp)]
        public async Task<IActionResult> Login(LoginParticipanteViewModel model)
        {
            ViewBag.Tab = "participantes";
            if (!ModelState.IsValid) return View(model);

            var correo = model.CorreoInstitucional.Trim().ToLowerInvariant();
            if (_limitador.EstaBloqueada(AmbitoLimite, correo, out var espera))
            {
                _logger.LogWarning("Login de participante rechazado por límite de cuenta. Correo: '{Correo}', IP: {IP}",
                    _limitador.NormalizarCuenta(correo), HttpContext.Connection.RemoteIpAddress);
                return new ResultadoLimiteExcedido(espera);
            }

            var registrante = await _context.Registrantes.FirstOrDefaultAsync(r => r.CorreoInstitucional == correo);
            var (valido, requiereRehash) = PasswordHashService.Verificar(model.Password, registrante?.PasswordHash);
            if (registrante == null || !valido)
            {
                _limitador.RegistrarFallo(AmbitoLimite, correo);
                _logger.LogWarning("Login fallido de participante. Correo intentado: '{Correo}', IP: {IP}", correo, HttpContext.Connection.RemoteIpAddress);
                ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
                return View(model);
            }

            _limitador.Reiniciar(AmbitoLimite, correo);
            if (!registrante.Activo)
            {
                ModelState.AddModelError(string.Empty, "Tu cuenta fue desactivada. Contacta a la administración del concurso.");
                return View(model);
            }

            if (requiereRehash)
            {
                registrante.PasswordHash = PasswordHashService.HashPassword(model.Password);
                await _context.SaveChangesAsync();
            }

            HttpContext.Session.IniciarRegistrante(registrante);
            return RedirectToAction(nameof(Panel));
        }

        [AccesoPublico]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        [AccesoPublico]
        public async Task<IActionResult> Signup() => View(await ConCatalogosAsync(new SignupViewModel()));

        [AccesoPublico]
        [HttpPost]
        public async Task<IActionResult> Signup(SignupViewModel model)
        {
            var registrante = await ValidarDatosPersonalesAsync(model, model.CorreoInstitucional);
            if (!ModelState.IsValid || registrante == null) return View(await ConCatalogosAsync(model));

            registrante.PasswordHash = PasswordHashService.HashPassword(model.Password);
            registrante.ProveedorAuth = "Local";
            if (!await GuardarRegistranteAsync(registrante)) return View(await ConCatalogosAsync(model));

            HttpContext.Session.IniciarRegistrante(registrante);
            return RedirectToAction(nameof(Panel));
        }

        [AccesoPublico]
        public IActionResult SolicitarJurado() => View(new SolicitudJuradoViewModel());

        // Auto-registro del jurado: la cuenta queda Pendiente hasta que la administracion la apruebe.
        [AccesoPublico]
        [HttpPost]
        public async Task<IActionResult> SolicitarJurado(SolicitudJuradoViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Password)) ModelState.AddModelError(nameof(model.Password), "Elige una contraseña.");
            var correo = model.CorreoInstitucional.Trim().ToLowerInvariant();
            if (!DominioInstitucional.EsCorreoInstitucional(correo)) ModelState.AddModelError(nameof(model.CorreoInstitucional), "Usa tu correo @uabc.edu.mx.");
            if (await _context.Jueces.AnyAsync(j => j.CorreoInstitucional == correo)) ModelState.AddModelError(nameof(model.CorreoInstitucional), "Ya existe una cuenta con ese correo.");
            if (!ModelState.IsValid) return View(model);

            if (!await GuardarSolicitudJuradoAsync(model.NombreCompleto, correo, model.Identificador, PasswordHashService.HashPassword(model.Password!), "Local"))
            {
                return View(model);
            }

            return RedirectToAction(nameof(Login), new { tab = "jurado", error = "Tu solicitud fue enviada. La administración debe aprobarla antes de que puedas entrar." });
        }

        // --- Google OAuth (Concurso.Core: email_verified, hd y token de identidad de un solo uso) ---

        [AccesoPublico]
        [EnableRateLimiting(PoliticasRateLimit.OAuthIp)]
        public IActionResult GoogleLogin(string intent)
        {
            var props = new AuthenticationProperties { RedirectUri = Url.Action(nameof(GoogleCallback)) };
            props.Items["intent"] = intent == "jurado" ? "jurado" : "participante";
            return Challenge(props, "Google");
        }

        [AccesoPublico]
        [HttpGet("Registro/google-listo")]
        [EnableRateLimiting(PoliticasRateLimit.OAuthIp)]
        public async Task<IActionResult> GoogleCallback()
        {
            var resultado = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!resultado.Succeeded || resultado.Principal == null)
            {
                return RedirectToAction(nameof(Login), new { error = "No se pudo completar el inicio de sesión con Google." });
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var intent = resultado.Properties?.Items.TryGetValue("intent", out var i) == true && i == "jurado" ? "jurado" : "participante";
            var tab = intent == "jurado" ? "jurado" : "participantes";
            var identidad = ValidacionIdentidadGoogle.Validar(resultado.Principal, _validacionGoogle.Value);
            if (!identidad.Valida)
            {
                _logger.LogWarning("Identidad de Google rechazada. Motivo: {Motivo}, IP: {IP}", identidad.Motivo, HttpContext.Connection.RemoteIpAddress);
                return RedirectToAction(nameof(Login), new
                {
                    tab,
                    error = ValidacionIdentidadGoogle.MensajeRechazo(identidad.Motivo)
                });
            }

            if (intent == "jurado")
            {
                var juez = await _context.Jueces.FirstOrDefaultAsync(j => j.CorreoInstitucional == identidad.Email);
                if (juez != null)
                {
                    if (!juez.Activo) return RedirectToAction(nameof(Login), new { tab, error = "Tu cuenta del jurado fue desactivada." });
                    if (juez.Pendiente) return RedirectToAction(nameof(Login), new { tab, error = "Tu solicitud aún está pendiente de aprobación." });
                    HttpContext.Session.IniciarJuez(juez);
                    return RedirectToAction("Index", "Evaluacion");
                }

                IdentidadGoogleVerificada.Guardar(HttpContext.Session, "jurado", identidad.Email, identidad.Nombre);
                return View("CompletarGoogleJurado", new SolicitudJuradoViewModel { NombreCompleto = identidad.Nombre, CorreoInstitucional = identidad.Email, DesdeGoogle = true });
            }

            var registrante = await _context.Registrantes.FirstOrDefaultAsync(r => r.CorreoInstitucional == identidad.Email);
            if (registrante != null)
            {
                if (!registrante.Activo) return RedirectToAction(nameof(Login), new { error = "Tu cuenta fue desactivada." });
                HttpContext.Session.IniciarRegistrante(registrante);
                return RedirectToAction(nameof(Panel));
            }

            IdentidadGoogleVerificada.Guardar(HttpContext.Session, "participante", identidad.Email, identidad.Nombre);
            return View("CompletarGoogle", await ConCatalogosAsync(new CompletarGoogleViewModel { NombreCompleto = identidad.Nombre, CorreoInstitucional = identidad.Email }));
        }

        [AccesoPublico]
        [HttpPost]
        [EnableRateLimiting(PoliticasRateLimit.OAuthIp)]
        public async Task<IActionResult> CompletarGoogle(CompletarGoogleViewModel model)
        {
            var (valido, email, nombre) = IdentidadGoogleVerificada.Obtener(HttpContext.Session, "participante");
            if (!valido)
            {
                return RedirectToAction(nameof(Login), new { error = "Tu sesión de Google expiró o no es válida. Intenta de nuevo con \"Continuar con Google\"." });
            }

            // El correo SIEMPRE es el verificado por Google; el nombre se puede ajustar.
            model.CorreoInstitucional = email;
            if (string.IsNullOrWhiteSpace(model.NombreCompleto)) model.NombreCompleto = nombre;
            var registrante = await ValidarDatosPersonalesAsync(model, email);
            if (!ModelState.IsValid || registrante == null) return View(await ConCatalogosAsync(model));

            registrante.ProveedorAuth = "Google";
            if (!await GuardarRegistranteAsync(registrante)) return View(await ConCatalogosAsync(model));

            IdentidadGoogleVerificada.Limpiar(HttpContext.Session);
            HttpContext.Session.IniciarRegistrante(registrante);
            return RedirectToAction(nameof(Panel));
        }

        [AccesoPublico]
        [HttpPost]
        [EnableRateLimiting(PoliticasRateLimit.OAuthIp)]
        public async Task<IActionResult> CompletarGoogleJurado(SolicitudJuradoViewModel model)
        {
            var (valido, email, _) = IdentidadGoogleVerificada.Obtener(HttpContext.Session, "jurado");
            if (!valido)
            {
                return RedirectToAction(nameof(Login), new { tab = "jurado", error = "Tu sesión de Google expiró o no es válida." });
            }

            model.CorreoInstitucional = email;
            model.DesdeGoogle = true;
            ModelState.Remove(nameof(model.CorreoInstitucional));
            if (await _context.Jueces.AnyAsync(j => j.CorreoInstitucional == email)) ModelState.AddModelError(string.Empty, "Ya existe una cuenta con ese correo.");
            if (!ModelState.IsValid) return View(model);

            // Cuentas de Google: la contraseña local es aleatoria e inutilizable (solo entran con Google).
            if (!await GuardarSolicitudJuradoAsync(model.NombreCompleto, email, model.Identificador, PasswordHashService.HashPassword(Guid.NewGuid().ToString("N")), "Google"))
            {
                return View(model);
            }

            IdentidadGoogleVerificada.Limpiar(HttpContext.Session);
            return RedirectToAction(nameof(Login), new { tab = "jurado", error = "Tu solicitud fue enviada. La administración debe aprobarla antes de que puedas entrar." });
        }

        // --- Panel del participante ---

        [SoloParticipante]
        public async Task<IActionResult> Panel()
        {
            var registranteId = HttpContext.Session.Registrante()!.Value;
            var registrante = await _context.Registrantes.Include(r => r.CatalogoCarrera).FirstOrDefaultAsync(r => r.Id == registranteId && r.Activo);
            if (registrante == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login));
            }

            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var config = await ConfiguracionCatrinas.ObtenerAsync(_context, periodo);
            var inscripcion = await _context.Inscripciones
                .Include(i => i.Carrera)
                .Include(i => i.MaestroEncargado)
                .Include(i => i.Evaluacion)
                .Include(i => i.Miembros).ThenInclude(m => m.Registrante)
                .AsSplitQuery()
                .FirstOrDefaultAsync(i => i.Activo && i.Periodo == periodo && i.Miembros.Any(m => m.RegistranteId == registranteId));

            var limiteInscripcion = FechaConcursoHelper.LimitePasado(config.FechaLimiteInscripcion);
            return View(new PanelParticipanteViewModel
            {
                Registrante = registrante,
                Inscripcion = inscripcion,
                Periodo = periodo,
                EsResponsable = inscripcion?.ResponsableId == registranteId,
                Faltantes = inscripcion == null ? Array.Empty<string>() : ReglasInscripcion.Faltantes(inscripcion),
                MotivoBloqueo = inscripcion == null ? null : ReglasInscripcion.MotivoEdicionBloqueada(inscripcion, config),
                PuedeCrear = inscripcion == null && !limiteInscripcion,
                MotivoNoPuedeCrear = limiteInscripcion
                    ? $"La inscripción cerró el {FechaConcursoHelper.Formatear(config.FechaLimiteInscripcion!.Value)}."
                    : null
            });
        }

        // --- Auxiliares ---

        private async Task<Registrante?> ValidarDatosPersonalesAsync(DatosPersonalesViewModel model, string correoCrudo)
        {
            var identificador = (model.Identificador ?? string.Empty).Trim();
            var correo = (correoCrudo ?? string.Empty).Trim().ToLowerInvariant();
            var tipo = identificador.Length == 7
                ? TipoRegistrante.Alumno
                : model.TipoPersonal == "Administrativo" ? TipoRegistrante.Administrativo : TipoRegistrante.Maestro;

            var registrante = new Registrante
            {
                Tipo = tipo,
                NombreCompleto = (model.NombreCompleto ?? string.Empty).Trim(),
                Identificador = identificador,
                CorreoInstitucional = correo,
                Telefono = string.IsNullOrWhiteSpace(model.Telefono) ? null : model.Telefono.Trim(),
                CatalogoGeneroId = model.CatalogoGeneroId is > 0 && await _context.Generos.AnyAsync(g => g.Id == model.CatalogoGeneroId && g.Activo) ? model.CatalogoGeneroId : null,
                CatalogoCarreraId = model.CatalogoCarreraId is > 0 && await _context.Carreras.AnyAsync(c => c.Id == model.CatalogoCarreraId && c.Activo) ? model.CatalogoCarreraId : null,
                AutodescripcionCultural = string.IsNullOrWhiteSpace(model.AutodescripcionCultural) ? null : model.AutodescripcionCultural.Trim()
            };

            foreach (var error in registrante.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(registrante)))
            {
                foreach (var miembro in error.MemberNames) ModelState.AddModelError(miembro, error.ErrorMessage ?? "Dato inválido.");
            }
            if (await _context.Registrantes.AnyAsync(r => r.Identificador == identificador))
            {
                ModelState.AddModelError(nameof(model.Identificador), "Ya existe una cuenta con esa matrícula o número de empleado.");
            }
            if (await _context.Registrantes.AnyAsync(r => r.CorreoInstitucional == correo))
            {
                ModelState.AddModelError("CorreoInstitucional", "Ya existe una cuenta con ese correo.");
            }

            return ModelState.IsValid ? registrante : null;
        }

        // El indice unico de la BD resuelve la carrera de dos registros simultaneos con los mismos datos.
        private async Task<bool> GuardarRegistranteAsync(Registrante registrante)
        {
            _context.Registrantes.Add(registrante);
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                _context.Entry(registrante).State = EntityState.Detached;
                ModelState.AddModelError(string.Empty, "Ya existe una cuenta con esa matrícula o número de empleado, o con ese correo.");
                return false;
            }
        }

        private async Task<bool> GuardarSolicitudJuradoAsync(string nombre, string correo, string identificador, string hash, string proveedor)
        {
            var juez = new Juez
            {
                NombreCompleto = nombre.Trim(),
                CorreoInstitucional = correo,
                Identificador = identificador.Trim(),
                Password = hash,
                ProveedorAuth = proveedor,
                Rol = RolesJuez.Juez,
                Pendiente = true
            };
            _context.Jueces.Add(juez);
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                _context.Entry(juez).State = EntityState.Detached;
                ModelState.AddModelError(string.Empty, "Ya existe una cuenta con ese correo.");
                return false;
            }
        }

        private async Task<T> ConCatalogosAsync<T>(T model) where T : DatosPersonalesViewModel
        {
            model.Generos = await _context.Generos.Where(g => g.Activo).OrderBy(g => g.Orden).ToListAsync();
            model.Carreras = await _context.Carreras.Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync();
            return model;
        }
    }
}
