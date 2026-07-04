using System.Security.Claims;
using AltarWeb.Models;
using AltarWeb.Models.Registro;
using AltarWeb.Services;
using AltarWeb.ViewModels.Altar;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Equipo = AltarWeb.Models.Registro.Equipo;

namespace AltarWeb.Controllers
{
    public class RegistroController : Controller
    {
        private readonly AltarDbContext _context;
        private readonly ILogger<RegistroController> _logger;

        public RegistroController(AltarDbContext context, ILogger<RegistroController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Login(string? error = null, string? tab = null)
        {
            ViewBag.Error = error;
            ViewBag.Tab = tab == "acceso" ? "acceso" : "registro";
            return View(new RegistroLoginViewModel());
        }

        [HttpPost]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(RegistroLoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var correo = model.CorreoInstitucional.Trim().ToLowerInvariant();
            var registrante = await _context.Registrantes
                .FirstOrDefaultAsync(r => r.CorreoInstitucional.ToLower() == correo);

            var (esValido, requiereRehash) = PasswordHashService.Verificar(model.Password, registrante?.PasswordHash);
            if (registrante == null || !esValido)
            {
                // LOG-01: registra el intento fallido (correo intentado + IP), nunca la contraseña.
                _logger.LogWarning("Login fallido de Registrante. Correo intentado: '{Correo}', IP: {IP}",
                    correo, HttpContext.Connection.RemoteIpAddress);
                ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
                return View(model);
            }

            if (!registrante.Activo)
            {
                ModelState.AddModelError(string.Empty, "Tu cuenta fue desactivada. Contacta a un administrador.");
                return View(model);
            }

            if (requiereRehash)
            {
                registrante.PasswordHash = PasswordHashService.HashPassword(model.Password);
                await _context.SaveChangesAsync();
            }

            CrearSesionRegistrante(registrante);
            return RedirectToAction("Dashboard");
        }

        public IActionResult Logout()
        {
            // SEC-14: Session.Clear() en vez de solo remover una clave — simetrico con AccesoController.Salir.
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> Signup()
        {
            return View(await ConstruirSignupViewModelAsync(new RegistroSignupViewModel()));
        }

        [HttpPost]
        public async Task<IActionResult> Signup(RegistroSignupViewModel model)
        {
            var identificador = model.Identificador.Trim();
            var tipoInferido = identificador.Length == 7 ? TipoRegistrante.Alumno : TipoRegistrante.Administrativo;

            var registrante = new Registrante
            {
                Tipo = tipoInferido,
                NombreCompleto = model.NombreCompleto.Trim(),
                Identificador = identificador,
                CorreoInstitucional = model.CorreoInstitucional.Trim().ToLowerInvariant(),
                Telefono = string.IsNullOrWhiteSpace(model.Telefono) ? null : model.Telefono.Trim(),
                CatalogoGeneroId = model.CatalogoGeneroId,
                CatalogoCarreraId = model.CatalogoCarreraId,
                AutodescripcionCultural = string.IsNullOrWhiteSpace(model.AutodescripcionCultural) ? null : model.AutodescripcionCultural.Trim()
            };

            var erroresDominio = registrante.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(registrante));
            foreach (var error in erroresDominio)
            {
                foreach (var member in error.MemberNames)
                {
                    ModelState.AddModelError(member, error.ErrorMessage ?? "Dato inválido.");
                }
            }

            if (await _context.Registrantes.AnyAsync(r => r.Identificador == registrante.Identificador))
            {
                ModelState.AddModelError(nameof(model.Identificador), "Ya existe un registrante con ese identificador.");
            }

            if (await _context.Registrantes.AnyAsync(r => r.CorreoInstitucional.ToLower() == registrante.CorreoInstitucional))
            {
                ModelState.AddModelError(nameof(model.CorreoInstitucional), "Ya existe un registrante con ese correo.");
            }

            if (!ModelState.IsValid)
            {
                return View(await ConstruirSignupViewModelAsync(model));
            }

            registrante.PasswordHash = PasswordHashService.HashPassword(model.Password);
            registrante.ProveedorAuth = "Local";

            _context.Registrantes.Add(registrante);
            await _context.SaveChangesAsync();

            // Si algun equipo tenia a este identificador como maestro encargado pendiente,
            // lo vinculamos ahora y lo promovemos a Maestro (regla explicita del comite).
            var equiposPendientes = await _context.EquiposConcurso
                .Where(e => e.MaestroEncargadoIdentificadorPendiente == registrante.Identificador)
                .ToListAsync();

            if (equiposPendientes.Count > 0)
            {
                registrante.Tipo = TipoRegistrante.Maestro;
                foreach (var equipo in equiposPendientes)
                {
                    equipo.MaestroEncargadoId = registrante.Id;
                    equipo.MaestroEncargadoIdentificadorPendiente = null;
                }
                await _context.SaveChangesAsync();
            }

            CrearSesionRegistrante(registrante);
            return RedirectToAction("Dashboard");
        }

        // --- Auto-registro de jueces ---

        public IActionResult SignupJuez()
        {
            return View(new RegistroSignupJuezViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> SignupJuez(RegistroSignupJuezViewModel model)
        {
            var identificador = model.Identificador.Trim();
            var correo = model.CorreoInstitucional.Trim().ToLowerInvariant();

            if (!correo.EndsWith("@uabc.edu.mx"))
            {
                ModelState.AddModelError(nameof(model.CorreoInstitucional), "Usa tu correo institucional @uabc.edu.mx.");
            }
            if (await _context.Jueces.IgnoreQueryFilters().AnyAsync(j => j.CorreoInstitucional == correo))
            {
                ModelState.AddModelError(nameof(model.CorreoInstitucional), "Ya existe una solicitud o cuenta con ese correo.");
            }
            if (await _context.Jueces.IgnoreQueryFilters().AnyAsync(j => j.Identificador == identificador))
            {
                ModelState.AddModelError(nameof(model.Identificador), "Ya existe una solicitud o cuenta con esa matrícula.");
            }

            if (!ModelState.IsValid) return View(model);

            var juez = new Juez
            {
                NombreCompleto = model.NombreCompleto.Trim(),
                CorreoInstitucional = correo,
                Identificador = identificador,
                Password = AccesoController.HashPassword(model.Password),
                ProveedorAuth = "Local",
                Rol = "Juez",
                Pendiente = true
            };

            _context.Jueces.Add(juez);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login", new { tab = "acceso", error = "Tu solicitud fue enviada. Un administrador debe aprobarla antes de que puedas iniciar sesión." });
        }

        // --- Google OAuth ---
        // intent = "registrante" (alumno/maestro/administrativo) o "juez".

        public IActionResult GoogleLogin(string intent)
        {
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleCallback", "Registro")
            };
            props.Items["intent"] = intent == "juez" ? "juez" : "registrante";
            return Challenge(props, "Google");
        }

        [HttpGet("Registro/google-callback")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> GoogleCallback()
        {
            var result = await HttpContext.AuthenticateAsync("Google");
            if (!result.Succeeded || result.Principal == null)
            {
                return RedirectToAction("Login", new { tab = "registro", error = "No se pudo completar el inicio de sesión con Google." });
            }

            var intent = (result.Properties?.Items.TryGetValue("intent", out var i) == true ? i : "registrante") ?? "registrante";
            var email = result.Principal.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
            var nombre = result.Principal.FindFirstValue(ClaimTypes.Name) ?? email ?? "Participante";
            var tabError = intent == "juez" ? "acceso" : "registro";

            if (string.IsNullOrWhiteSpace(email) || !email.EndsWith("@uabc.edu.mx"))
            {
                return RedirectToAction("Login", new { tab = tabError, error = "Debes usar tu correo institucional @uabc.edu.mx." });
            }

            if (intent == "juez")
            {
                var juez = await _context.Jueces.FirstOrDefaultAsync(j => j.CorreoInstitucional == email);
                if (juez != null)
                {
                    if (juez.Pendiente)
                    {
                        return RedirectToAction("Login", new { tab = "acceso", error = "Tu solicitud aún está pendiente de aprobación por un administrador." });
                    }

                    HttpContext.Session.SetInt32("JuezId", juez.Id);
                    HttpContext.Session.SetString("JuezNombre", juez.NombreCompleto);
                    HttpContext.Session.SetString("JuezRol", juez.Rol);
                    return RedirectToAction("Historial", "AltarEvaluacion");
                }

                GuardarIdentidadGoogleVerificada("juez", email, nombre);
                return View("CompletarGoogleJuez", new CompletarGoogleJuezViewModel { NombreCompleto = nombre, CorreoInstitucional = email });
            }
            else
            {
                var registrante = await _context.Registrantes.FirstOrDefaultAsync(r => r.CorreoInstitucional.ToLower() == email);
                if (registrante != null)
                {
                    if (!registrante.Activo)
                    {
                        return RedirectToAction("Login", new { tab = "registro", error = "Tu cuenta fue desactivada. Contacta a un administrador." });
                    }

                    CrearSesionRegistrante(registrante);
                    return RedirectToAction("Dashboard");
                }

                GuardarIdentidadGoogleVerificada("registrante", email, nombre);
                var vm = new CompletarGoogleViewModel { NombreCompleto = nombre, CorreoInstitucional = email };
                vm.Generos = await _context.CatalogoGeneros.Where(g => g.Activo).OrderBy(g => g.Orden).ToListAsync();
                vm.Carreras = await _context.CatalogoCarreras.Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync();
                return View("CompletarGoogle", vm);
            }
        }

        // SEC-01: el email/nombre de estas vistas viajan como campo oculto del formulario, asi que no
        // son confiables en el POST (un atacante puede postear cualquier valor sin haber pasado por
        // Google). Los guardamos aqui, server-side, justo despues de validar el ticket de Google, y los
        // POST de CompletarGoogle*/leen exclusivamente de esta fuente (ver ObtenerIdentidadGoogleVerificada).
        private void GuardarIdentidadGoogleVerificada(string intent, string email, string nombre)
        {
            HttpContext.Session.SetString("GooglePendienteIntent", intent);
            HttpContext.Session.SetString("GooglePendienteEmail", email);
            HttpContext.Session.SetString("GooglePendienteNombre", nombre);
            HttpContext.Session.SetString("GooglePendienteExpira", DateTime.UtcNow.Add(TimeSpan.FromMinutes(10)).ToString("O"));
        }

        // Token de un solo uso de vida corta: exige que el intent coincida con el formulario que se esta
        // procesando y que no hayan pasado mas de 10 minutos desde el callback de Google.
        private (bool Valido, string Email, string Nombre) ObtenerIdentidadGoogleVerificada(string intentEsperado)
        {
            var intent = HttpContext.Session.GetString("GooglePendienteIntent");
            var email = HttpContext.Session.GetString("GooglePendienteEmail");
            var nombre = HttpContext.Session.GetString("GooglePendienteNombre");
            var expiraRaw = HttpContext.Session.GetString("GooglePendienteExpira");

            if (intent != intentEsperado || string.IsNullOrEmpty(email)
                || !DateTime.TryParse(expiraRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expira)
                || DateTime.UtcNow > expira)
            {
                return (false, string.Empty, string.Empty);
            }

            return (true, email, nombre ?? email);
        }

        private void LimpiarIdentidadGoogleVerificada()
        {
            HttpContext.Session.Remove("GooglePendienteIntent");
            HttpContext.Session.Remove("GooglePendienteEmail");
            HttpContext.Session.Remove("GooglePendienteNombre");
            HttpContext.Session.Remove("GooglePendienteExpira");
        }

        [HttpPost]
        public async Task<IActionResult> CompletarGoogle(CompletarGoogleViewModel model)
        {
            var (googleValido, emailVerificado, nombreVerificado) = ObtenerIdentidadGoogleVerificada("registrante");
            if (!googleValido)
            {
                return RedirectToAction("Login", new { tab = "registro", error = "Tu sesión de Google expiró o no es válida. Intenta de nuevo con \"Continuar con Google\"." });
            }

            var identificador = model.Identificador.Trim();
            var tipoInferido = identificador.Length == 7 ? TipoRegistrante.Alumno : TipoRegistrante.Administrativo;

            var registrante = new Registrante
            {
                Tipo = tipoInferido,
                NombreCompleto = nombreVerificado.Trim(),
                Identificador = identificador,
                CorreoInstitucional = emailVerificado,
                Telefono = string.IsNullOrWhiteSpace(model.Telefono) ? null : model.Telefono.Trim(),
                CatalogoGeneroId = model.CatalogoGeneroId,
                CatalogoCarreraId = model.CatalogoCarreraId,
                AutodescripcionCultural = string.IsNullOrWhiteSpace(model.AutodescripcionCultural) ? null : model.AutodescripcionCultural.Trim()
            };

            var erroresDominio = registrante.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(registrante));
            foreach (var error in erroresDominio)
            {
                foreach (var member in error.MemberNames)
                {
                    ModelState.AddModelError(member, error.ErrorMessage ?? "Dato inválido.");
                }
            }

            if (await _context.Registrantes.AnyAsync(r => r.Identificador == registrante.Identificador))
            {
                ModelState.AddModelError(nameof(model.Identificador), "Ya existe un registrante con ese identificador.");
            }

            if (!ModelState.IsValid)
            {
                model.NombreCompleto = nombreVerificado;
                model.CorreoInstitucional = emailVerificado;
                model.Generos = await _context.CatalogoGeneros.Where(g => g.Activo).OrderBy(g => g.Orden).ToListAsync();
                model.Carreras = await _context.CatalogoCarreras.Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync();
                return View(model);
            }

            registrante.PasswordHash = null;
            registrante.ProveedorAuth = "Google";

            _context.Registrantes.Add(registrante);
            await _context.SaveChangesAsync();

            var equiposPendientes = await _context.EquiposConcurso
                .Where(e => e.MaestroEncargadoIdentificadorPendiente == registrante.Identificador)
                .ToListAsync();

            if (equiposPendientes.Count > 0)
            {
                registrante.Tipo = TipoRegistrante.Maestro;
                foreach (var equipo in equiposPendientes)
                {
                    equipo.MaestroEncargadoId = registrante.Id;
                    equipo.MaestroEncargadoIdentificadorPendiente = null;
                }
                await _context.SaveChangesAsync();
            }

            LimpiarIdentidadGoogleVerificada();
            CrearSesionRegistrante(registrante);
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> CompletarGoogleJuez(CompletarGoogleJuezViewModel model)
        {
            var (googleValido, emailVerificado, nombreVerificado) = ObtenerIdentidadGoogleVerificada("juez");
            if (!googleValido)
            {
                return RedirectToAction("Login", new { tab = "acceso", error = "Tu sesión de Google expiró o no es válida. Intenta de nuevo con \"Continuar con Google\"." });
            }

            var identificador = model.Identificador.Trim();
            var correo = emailVerificado;

            if (await _context.Jueces.IgnoreQueryFilters().AnyAsync(j => j.Identificador == identificador))
            {
                ModelState.AddModelError(nameof(model.Identificador), "Ya existe una solicitud o cuenta con esa matrícula.");
            }

            if (!ModelState.IsValid)
            {
                model.NombreCompleto = nombreVerificado;
                model.CorreoInstitucional = correo;
                return View(model);
            }

            var juez = new Juez
            {
                NombreCompleto = nombreVerificado.Trim(),
                CorreoInstitucional = correo,
                Identificador = identificador,
                Password = AccesoController.HashPassword(Guid.NewGuid().ToString()),
                ProveedorAuth = "Google",
                Rol = "Juez",
                Pendiente = true
            };

            _context.Jueces.Add(juez);
            await _context.SaveChangesAsync();

            LimpiarIdentidadGoogleVerificada();
            return RedirectToAction("Login", new { tab = "acceso", error = "Tu solicitud fue enviada. Un administrador debe aprobarla antes de que puedas iniciar sesión." });
        }

        public async Task<IActionResult> Dashboard()
        {
            var registrante = await ObtenerRegistranteSesionAsync();
            if (registrante == null) return RedirectToAction("Login");

            var periodo = PeriodoHelper.ObtenerPeriodoActual();

            var equipo = await _context.EquiposConcurso
                .Include(e => e.Carrera)
                .Include(e => e.MaestroEncargado)
                .Include(e => e.Difunto)
                .Include(e => e.Integrantes).ThenInclude(ri => ri.Registrante)
                .Include(e => e.Evaluacion)
                .Where(e => e.Periodo == periodo && e.Integrantes.Any(ri => ri.RegistranteId == registrante.Id))
                .FirstOrDefaultAsync();

            var elementos = await _context.Elementos
                .Where(el => el.Activo)
                .OrderBy(el => el.Orden)
                .ToListAsync();

            return View(new RegistranteDashboardViewModel
            {
                Registrante = registrante,
                Equipo = equipo,
                EsOrganizador = equipo != null && equipo.CreadoPorRegistranteId == registrante.Id,
                FichaCompleta = equipo != null && EquipoValidacionHelper.EsFichaCompleta(equipo),
                EquipoEvaluado = equipo?.Evaluacion?.Estado == EstadoEvaluacion.Final,
                Elementos = elementos
            });
        }

        public async Task<IActionResult> CrearEquipo()
        {
            var registrante = await ObtenerRegistranteSesionAsync();
            if (registrante == null) return RedirectToAction("Login");

            if (await TieneEquipoActivoAsync(registrante.Id))
            {
                TempData["Error"] = "Ya perteneces a un equipo este periodo.";
                return RedirectToAction("Dashboard");
            }

            if (await FechaLimiteInscripcionPasadaAsync(PeriodoHelper.ObtenerPeriodoActual()))
            {
                TempData["Error"] = "La fecha límite de inscripción de equipos para este periodo ya pasó.";
                return RedirectToAction("Dashboard");
            }

            return View(await ConstruirCrearEquipoViewModelAsync(new CrearEquipoViewModel()));
        }

        [HttpPost]
        public async Task<IActionResult> CrearEquipo(CrearEquipoViewModel model)
        {
            var registrante = await ObtenerRegistranteSesionAsync();
            if (registrante == null) return RedirectToAction("Login");

            if (await TieneEquipoActivoAsync(registrante.Id))
            {
                TempData["Error"] = "Ya perteneces a un equipo este periodo.";
                return RedirectToAction("Dashboard");
            }

            var periodo = PeriodoHelper.ObtenerPeriodoActual();

            if (await FechaLimiteInscripcionPasadaAsync(periodo))
            {
                TempData["Error"] = "La fecha límite de inscripción de equipos para este periodo ya pasó.";
                return RedirectToAction("Dashboard");
            }

            var nombre = model.Nombre.Trim();
            if (await _context.EquiposConcurso.AnyAsync(e => e.Nombre == nombre && e.Periodo == periodo))
            {
                ModelState.AddModelError(nameof(model.Nombre), "Ya existe un equipo con ese nombre en este periodo.");
            }

            var (maestroId, maestroPendiente, maestroError) = await ResolverMaestroEncargadoAsync(model.MaestroEncargadoIdentificador);
            if (maestroError != null)
            {
                ModelState.AddModelError(nameof(model.MaestroEncargadoIdentificador), maestroError);
            }

            await ValidarFechaDefuncionAsync(periodo, model.DifuntoFechaDefuncion, nameof(model.DifuntoFechaDefuncion));

            if (!ModelState.IsValid)
            {
                return View(await ConstruirCrearEquipoViewModelAsync(model));
            }

            var equipo = new Equipo
            {
                Nombre = nombre,
                NombreAltar = model.NombreAltar.Trim(),
                CarreraId = model.CarreraId,
                Periodo = periodo,
                CreadoPorRegistranteId = registrante.Id,
                MaestroEncargadoId = maestroId,
                MaestroEncargadoIdentificadorPendiente = maestroPendiente,
                UbicacionAltar = string.IsNullOrWhiteSpace(model.UbicacionAltar) ? null : model.UbicacionAltar.Trim(),
                HaceCatrina = model.HaceCatrina
            };

            // SEC-11: transaccion explicita para las 3 escrituras (Equipo, Difunto, RegistranteEquipo).
            // Sin ella, una race que viole el indice unico (RegistranteId, Periodo) en el ultimo insert
            // dejaba el Equipo (y su Difunto) huerfanos y persistidos aunque el organizador nunca quedara
            // vinculado — confirmado con una prueba real de dos requests concurrentes en la misma sesion.
            using var transaction = await _context.Database.BeginTransactionAsync();

            _context.EquiposConcurso.Add(equipo);
            await _context.SaveChangesAsync();

            _context.Difuntos.Add(new Difunto
            {
                EquipoId = equipo.Id,
                Nombre = model.DifuntoNombre.Trim(),
                FechaDefuncion = model.DifuntoFechaDefuncion,
                TipoAltar = TipoAltar.Tradicional
            });

            _context.RegistranteEquipos.Add(new RegistranteEquipo
            {
                RegistranteId = registrante.Id,
                EquipoId = equipo.Id,
                Rol = RolEquipo.Integrante,
                Periodo = periodo
            });

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                // Race rara (dos requests simultaneos del mismo registrante, o el candidato se unio a
                // otro equipo justo entre el check y el insert); la BD lo bloqueo via el indice unico
                // (RegistranteId, Periodo). Rollback completo: no debe quedar un Equipo/Difunto huerfano.
                await transaction.RollbackAsync();
                TempData["Error"] = "Ya perteneces a un equipo este periodo.";
                return RedirectToAction("Dashboard");
            }

            TempData["Mensaje"] = "Equipo creado. Ya puedes agregar integrantes y designar un narrador.";
            return RedirectToAction("Ficha");
        }

        public async Task<IActionResult> Ficha()
        {
            var registrante = await ObtenerRegistranteSesionAsync();
            if (registrante == null) return RedirectToAction("Login");

            var equipo = await ObtenerEquipoOrganizadorAsync(registrante.Id);
            if (equipo == null)
            {
                TempData["Error"] = "No eres organizador de ningún equipo activo.";
                return RedirectToAction("Dashboard");
            }

            return View(await ConstruirFichaViewModelAsync(equipo, registrante));
        }

        [HttpPost]
        public async Task<IActionResult> Ficha(FichaViewModel model)
        {
            var registrante = await ObtenerRegistranteSesionAsync();
            if (registrante == null) return RedirectToAction("Login");

            var equipo = await ObtenerEquipoOrganizadorAsync(registrante.Id);
            if (equipo == null || equipo.Id != model.EquipoId)
            {
                TempData["Error"] = "No eres organizador de ningún equipo activo.";
                return RedirectToAction("Dashboard");
            }

            if (equipo.Evaluacion?.Estado == EstadoEvaluacion.Final)
            {
                TempData["Error"] = "El equipo ya fue evaluado en definitiva; la ficha no puede editarse.";
                return RedirectToAction("Dashboard");
            }

            if (await FechaLimiteRequisitosPasadaAsync(equipo.Periodo))
            {
                TempData["Error"] = "La fecha límite para completar los requisitos ya pasó; la ficha no puede editarse. Contacta a un administrador.";
                return RedirectToAction("Dashboard");
            }

            var nombre = model.Nombre.Trim();
            if (await _context.EquiposConcurso.AnyAsync(e => e.Id != equipo.Id && e.Nombre == nombre && e.Periodo == equipo.Periodo))
            {
                ModelState.AddModelError(nameof(model.Nombre), "Ya existe un equipo con ese nombre en este periodo.");
            }

            var (maestroId, maestroPendiente, maestroError) = await ResolverMaestroEncargadoAsync(model.MaestroEncargadoIdentificador);
            if (maestroError != null)
            {
                ModelState.AddModelError(nameof(model.MaestroEncargadoIdentificador), maestroError);
            }

            await ValidarFechaDefuncionAsync(equipo.Periodo, model.DifuntoFechaDefuncion, nameof(model.DifuntoFechaDefuncion));

            if (!ModelState.IsValid)
            {
                return View(await ConstruirFichaViewModelAsync(equipo, registrante, model));
            }

            equipo.Nombre = nombre;
            equipo.NombreAltar = model.NombreAltar.Trim();
            equipo.CarreraId = model.CarreraId;
            equipo.MaestroEncargadoId = maestroId;
            equipo.MaestroEncargadoIdentificadorPendiente = maestroPendiente;
            equipo.UbicacionAltar = string.IsNullOrWhiteSpace(model.UbicacionAltar) ? null : model.UbicacionAltar.Trim();
            equipo.HaceCatrina = model.HaceCatrina;

            if (equipo.Difunto != null)
            {
                equipo.Difunto.Nombre = model.DifuntoNombre.Trim();
                equipo.Difunto.FechaDefuncion = model.DifuntoFechaDefuncion;
            }

            await _context.SaveChangesAsync();
            TempData["Mensaje"] = "Ficha de Registro del Altar actualizada.";
            return RedirectToAction("Ficha");
        }

        [HttpPost]
        public async Task<IActionResult> AgregarIntegrante(string identificador)
        {
            var registrante = await ObtenerRegistranteSesionAsync();
            if (registrante == null) return RedirectToAction("Login");

            var equipo = await ObtenerEquipoOrganizadorAsync(registrante.Id);
            if (equipo == null || equipo.Evaluacion?.Estado == EstadoEvaluacion.Final)
            {
                TempData["Error"] = "No puedes modificar integrantes de este equipo.";
                return RedirectToAction("Ficha");
            }

            var candidato = await _context.Registrantes.FirstOrDefaultAsync(r => r.Identificador == identificador.Trim());
            if (candidato == null)
            {
                TempData["Error"] = "No existe un registrante con ese identificador.";
                return RedirectToAction("Ficha");
            }

            if (await TieneEquipoActivoAsync(candidato.Id))
            {
                TempData["Error"] = $"{candidato.NombreCompleto} ya pertenece a un equipo este periodo.";
                return RedirectToAction("Ficha");
            }

            _context.RegistranteEquipos.Add(new RegistranteEquipo
            {
                RegistranteId = candidato.Id,
                EquipoId = equipo.Id,
                Rol = RolEquipo.Integrante,
                Periodo = equipo.Periodo
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // SEC-11: race rara (el candidato se unio a otro equipo justo entre el check y el insert).
                TempData["Error"] = $"{candidato.NombreCompleto} ya pertenece a un equipo este periodo.";
                return RedirectToAction("Ficha");
            }

            TempData["Mensaje"] = $"{candidato.NombreCompleto} fue agregado al equipo.";
            return RedirectToAction("Ficha");
        }

        [HttpPost]
        public async Task<IActionResult> QuitarIntegrante(int registranteId)
        {
            var registrante = await ObtenerRegistranteSesionAsync();
            if (registrante == null) return RedirectToAction("Login");

            var equipo = await ObtenerEquipoOrganizadorAsync(registrante.Id);
            if (equipo == null || equipo.Evaluacion?.Estado == EstadoEvaluacion.Final)
            {
                TempData["Error"] = "No puedes modificar integrantes de este equipo.";
                return RedirectToAction("Ficha");
            }

            if (registranteId == equipo.CreadoPorRegistranteId)
            {
                TempData["Error"] = "El organizador no puede quitarse a sí mismo.";
                return RedirectToAction("Ficha");
            }

            var vinculo = equipo.Integrantes.FirstOrDefault(i => i.RegistranteId == registranteId);
            if (vinculo != null)
            {
                _context.RegistranteEquipos.Remove(vinculo);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Ficha");
        }

        [HttpPost]
        public async Task<IActionResult> DesignarNarrador(int registranteId)
        {
            var registrante = await ObtenerRegistranteSesionAsync();
            if (registrante == null) return RedirectToAction("Login");

            var equipo = await ObtenerEquipoOrganizadorAsync(registrante.Id);
            if (equipo == null || equipo.Evaluacion?.Estado == EstadoEvaluacion.Final)
            {
                TempData["Error"] = "No puedes modificar integrantes de este equipo.";
                return RedirectToAction("Ficha");
            }

            foreach (var integrante in equipo.Integrantes)
            {
                integrante.Rol = integrante.RegistranteId == registranteId ? RolEquipo.Narrador : RolEquipo.Integrante;
            }

            await _context.SaveChangesAsync();
            TempData["Mensaje"] = "Narrador designado.";
            return RedirectToAction("Ficha");
        }

        private async Task<bool> TieneEquipoActivoAsync(int registranteId)
        {
            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            return await _context.RegistranteEquipos
                .AnyAsync(ri => ri.RegistranteId == registranteId && ri.Equipo.Periodo == periodo);
        }

        private async Task<Equipo?> ObtenerEquipoOrganizadorAsync(int registranteId)
        {
            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            return await _context.EquiposConcurso
                .Include(e => e.Difunto)
                .Include(e => e.Evaluacion)
                .Include(e => e.MaestroEncargado)
                .Include(e => e.Integrantes).ThenInclude(ri => ri.Registrante)
                .FirstOrDefaultAsync(e => e.Periodo == periodo && e.CreadoPorRegistranteId == registranteId);
        }

        // Resuelve el identificador de maestro capturado por el alumno: si ya existe un
        // Registrante con ese identificador (Maestro o Administrativo) lo vincula directo y lo
        // promueve a Maestro; si no existe todavia, lo deja pendiente hasta que se registre.
        private async Task<(int? MaestroId, string? Pendiente, string? Error)> ResolverMaestroEncargadoAsync(string identificadorCrudo)
        {
            var identificador = (identificadorCrudo ?? string.Empty).Trim();

            if (identificador.Length != 5 || !identificador.All(char.IsDigit))
            {
                return (null, null, "La matrícula o número de empleado del maestro encargado debe tener exactamente 5 dígitos.");
            }

            var candidato = await _context.Registrantes.FirstOrDefaultAsync(r => r.Identificador == identificador);
            if (candidato == null)
            {
                return (null, identificador, null);
            }

            if (candidato.Tipo == TipoRegistrante.Alumno)
            {
                return (null, null, "Ese identificador pertenece a un alumno; el maestro encargado debe ser maestro o administrativo.");
            }

            if (candidato.Tipo == TipoRegistrante.Administrativo)
            {
                candidato.Tipo = TipoRegistrante.Maestro;
            }

            return (candidato.Id, null, null);
        }

        private async Task ValidarFechaDefuncionAsync(string periodo, DateOnly fecha, string campo)
        {
            var config = await _context.ConfiguracionesPeriodo.FirstOrDefaultAsync(c => c.Periodo == periodo);
            if (config?.ExigirMinimoUnAnioFallecimiento != true) return;

            var limite = DateOnly.FromDateTime(DateTime.Today.AddYears(-1));
            if (fecha > limite)
            {
                ModelState.AddModelError(campo, "La fecha de defunción debe ser de al menos 1 año atrás.");
            }
        }

        private async Task<CrearEquipoViewModel> ConstruirCrearEquipoViewModelAsync(CrearEquipoViewModel model)
        {
            model.Carreras = await _context.CatalogoCarreras.Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync();
            return model;
        }

        private async Task<FichaViewModel> ConstruirFichaViewModelAsync(Equipo equipo, Registrante organizador, FichaViewModel? model = null)
        {
            var vm = model ?? new FichaViewModel
            {
                Nombre = equipo.Nombre,
                NombreAltar = equipo.NombreAltar,
                DifuntoNombre = equipo.Difunto?.Nombre ?? string.Empty,
                DifuntoFechaDefuncion = equipo.Difunto?.FechaDefuncion ?? DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
                CarreraId = equipo.CarreraId,
                MaestroEncargadoIdentificador = equipo.MaestroEncargado?.Identificador ?? equipo.MaestroEncargadoIdentificadorPendiente ?? string.Empty,
                UbicacionAltar = equipo.UbicacionAltar,
                HaceCatrina = equipo.HaceCatrina
            };

            vm.EquipoId = equipo.Id;
            vm.CreadoPorRegistranteId = equipo.CreadoPorRegistranteId;
            vm.ResponsableNombre = organizador.NombreCompleto;
            vm.ResponsableTelefono = organizador.Telefono;
            vm.ResponsableCorreo = organizador.CorreoInstitucional;
            vm.CreadoEn = equipo.CreadoEn;
            vm.Integrantes = equipo.Integrantes.ToList();
            vm.MaestroEncargadoNombre = equipo.MaestroEncargado?.NombreCompleto;
            vm.MaestroEncargadoPendiente = equipo.MaestroEncargado == null && !string.IsNullOrEmpty(equipo.MaestroEncargadoIdentificadorPendiente);
            vm.Carreras = await _context.CatalogoCarreras.Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync();

            var periodo = equipo.Periodo;
            var config = await _context.ConfiguracionesPeriodo.FirstOrDefaultAsync(c => c.Periodo == periodo);
            vm.ExigirMinimoUnAnioFallecimiento = config?.ExigirMinimoUnAnioFallecimiento ?? true;
            vm.FechaLimiteRequisitosPasada = FechaPasada(config?.FechaLimiteRequisitos);
            vm.PuedeEditar = equipo.Evaluacion?.Estado != EstadoEvaluacion.Final && !vm.FechaLimiteRequisitosPasada;

            return vm;
        }

        // Los <input type="date"> guardan solo el dia; comparar por Date trata el dia limite
        // como incluido completo (vence al empezar el dia siguiente), no desde la medianoche.
        private static bool FechaPasada(DateTime? limite) => limite != null && DateTime.Now.Date > limite.Value.Date;

        private async Task<bool> FechaLimiteInscripcionPasadaAsync(string periodo)
        {
            var config = await _context.ConfiguracionesPeriodo.FirstOrDefaultAsync(c => c.Periodo == periodo);
            return FechaPasada(config?.FechaLimiteInscripcion);
        }

        private async Task<bool> FechaLimiteRequisitosPasadaAsync(string periodo)
        {
            var config = await _context.ConfiguracionesPeriodo.FirstOrDefaultAsync(c => c.Periodo == periodo);
            return FechaPasada(config?.FechaLimiteRequisitos);
        }

        private async Task<RegistroSignupViewModel> ConstruirSignupViewModelAsync(RegistroSignupViewModel model)
        {
            model.Generos = await _context.CatalogoGeneros.Where(g => g.Activo).OrderBy(g => g.Orden).ToListAsync();
            model.Carreras = await _context.CatalogoCarreras.Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync();
            return model;
        }

        private void CrearSesionRegistrante(Registrante registrante)
        {
            HttpContext.Session.SetInt32("RegistranteId", registrante.Id);
        }

        private async Task<Registrante?> ObtenerRegistranteSesionAsync()
        {
            var id = HttpContext.Session.GetInt32("RegistranteId");
            if (id == null) return null;
            return await _context.Registrantes
                .Include(r => r.CatalogoCarrera)
                .FirstOrDefaultAsync(r => r.Id == id.Value);
        }

    }
}
