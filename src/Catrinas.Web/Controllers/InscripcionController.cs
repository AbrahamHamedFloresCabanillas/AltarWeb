using Catrinas.Web.Datos;
using Catrinas.Web.Modelos;
using Catrinas.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Web.Controllers
{
    // Inscripcion de Catrina (ADR §7.1): la crea el responsable, que administra integrantes y designa a
    // quien personifica. Se cierra al existir evaluacion del jurado o pasada la fecha limite de requisitos.
    [SoloParticipante]
    public class InscripcionController : Controller
    {
        private readonly CatrinasDbContext _context;

        public InscripcionController(CatrinasDbContext context) => _context = context;

        private int RegistranteId => HttpContext.Session.Registrante()!.Value;

        public async Task<IActionResult> Crear()
        {
            var bloqueo = await MotivoNoPuedeCrearAsync(PeriodoHelper.ObtenerPeriodoActual());
            if (bloqueo != null)
            {
                TempData["Error"] = bloqueo;
                return RedirectToAction("Panel", "Registro");
            }
            return View(await ConCarrerasAsync(new InscripcionFormViewModel()));
        }

        [HttpPost]
        public async Task<IActionResult> Crear(InscripcionFormViewModel model)
        {
            var periodo = PeriodoHelper.ObtenerPeriodoActual();
            var bloqueo = await MotivoNoPuedeCrearAsync(periodo);
            if (bloqueo != null)
            {
                TempData["Error"] = bloqueo;
                return RedirectToAction("Panel", "Registro");
            }

            var nombre = (model.Nombre ?? string.Empty).Trim();
            var datos = await ValidarFormularioAsync(model, nombre, periodo, idActual: null);
            if (!ModelState.IsValid) return View(await ConCarrerasAsync(model));

            var inscripcion = new Inscripcion
            {
                Nombre = nombre,
                CarreraId = model.CarreraId,
                Periodo = periodo,
                ResponsableId = RegistranteId,
                MaestroEncargadoId = datos.MaestroId,
                AltarExternalId = datos.AltarId,
                AltarNombreSnapshot = datos.AltarNombre
            };
            inscripcion.Miembros.Add(new MiembroInscripcion { RegistranteId = RegistranteId, Rol = RolInscripcion.Integrante, Periodo = periodo });
            _context.Inscripciones.Add(inscripcion);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Carrera real: otra inscripcion con el mismo nombre, o la persona se unio a otra inscripcion
                // del periodo entre la validacion y el guardado. Los indices unicos de la BD lo impiden.
                TempData["Error"] = "No se pudo crear la inscripción: ya existe una con ese nombre o ya perteneces a una inscripción este periodo.";
                return RedirectToAction("Panel", "Registro");
            }

            TempData["Mensaje"] = "Inscripción creada. Agrega a tus integrantes y designa a quien personificará a la Catrina.";
            return RedirectToAction(nameof(Editar));
        }

        public async Task<IActionResult> Editar()
        {
            var inscripcion = await ObtenerPropiaAsync();
            if (inscripcion == null)
            {
                TempData["Error"] = "No eres responsable de ninguna inscripción activa este periodo.";
                return RedirectToAction("Panel", "Registro");
            }

            var config = await ConfiguracionCatrinas.ObtenerAsync(_context, inscripcion.Periodo);
            ViewBag.Inscripcion = inscripcion;
            ViewBag.MotivoBloqueo = ReglasInscripcion.MotivoEdicionBloqueada(inscripcion, config);
            ViewBag.Faltantes = ReglasInscripcion.Faltantes(inscripcion);
            ViewBag.Invitaciones = await InvitacionesPendientesAsync(inscripcion.Id);
            return View(await ConCarrerasAsync(new InscripcionFormViewModel
            {
                Id = inscripcion.Id,
                Nombre = inscripcion.Nombre,
                CarreraId = inscripcion.CarreraId,
                MaestroEncargadoIdentificador = inscripcion.MaestroEncargado?.Identificador,
                AltarExternalId = inscripcion.AltarExternalId,
                AltarNombreSnapshot = inscripcion.AltarNombreSnapshot
            }));
        }

        [HttpPost]
        public async Task<IActionResult> Editar(InscripcionFormViewModel model)
        {
            var inscripcion = await ObtenerPropiaAsync();
            if (inscripcion == null) return RedirectToAction("Panel", "Registro");
            if (await RechazarSiBloqueadaAsync(inscripcion) is { } rechazo) return rechazo;

            var nombre = (model.Nombre ?? string.Empty).Trim();
            var datos = await ValidarFormularioAsync(model, nombre, inscripcion.Periodo, inscripcion.Id);
            if (!ModelState.IsValid)
            {
                var config = await ConfiguracionCatrinas.ObtenerAsync(_context, inscripcion.Periodo);
                ViewBag.Inscripcion = inscripcion;
                ViewBag.MotivoBloqueo = ReglasInscripcion.MotivoEdicionBloqueada(inscripcion, config);
                ViewBag.Faltantes = ReglasInscripcion.Faltantes(inscripcion);
                ViewBag.Invitaciones = await InvitacionesPendientesAsync(inscripcion.Id);
                return View(await ConCarrerasAsync(model));
            }

            inscripcion.Nombre = nombre;
            inscripcion.CarreraId = model.CarreraId;
            inscripcion.MaestroEncargadoId = datos.MaestroId;
            inscripcion.AltarExternalId = datos.AltarId;
            inscripcion.AltarNombreSnapshot = datos.AltarNombre;
            inscripcion.ActualizadoEn = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Ya existe otra inscripción con ese nombre en el periodo.";
                return RedirectToAction(nameof(Editar));
            }

            TempData["Mensaje"] = "Inscripción actualizada.";
            return RedirectToAction(nameof(Editar));
        }

        [HttpPost]
        public async Task<IActionResult> AgregarIntegrante(string identificador)
        {
            var inscripcion = await ObtenerPropiaAsync();
            if (inscripcion == null) return RedirectToAction("Panel", "Registro");
            if (await RechazarSiBloqueadaAsync(inscripcion) is { } rechazo) return rechazo;

            // A1 (pruebas adversariales 2026-10-21; cierra el riesgo M1 aceptado en ADR §13): ya no se agrega
            // directamente. Se crea una invitacion que la persona acepta o rechaza desde su panel, y la respuesta es
            // la misma exista o no la cuenta, tenga o no inscripcion (el formulario no sirve de buscador de personas).
            var id = (identificador ?? string.Empty).Trim();
            var registrante = await _context.Registrantes.FirstOrDefaultAsync(r => r.Identificador == id && r.Activo);
            if (registrante != null && registrante.Id != RegistranteId
                && !await _context.Miembros.AnyAsync(m => m.RegistranteId == registrante.Id && m.Periodo == inscripcion.Periodo)
                && !await _context.Invitaciones.AnyAsync(i => i.InscripcionId == inscripcion.Id && i.RegistranteId == registrante.Id))
            {
                _context.Invitaciones.Add(new InvitacionInscripcion { InscripcionId = inscripcion.Id, RegistranteId = registrante.Id, Periodo = inscripcion.Periodo });
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Doble envio del mismo formulario: el indice unico (InscripcionId, RegistranteId) ya la tiene.
                }
            }

            TempData["Mensaje"] = "Si esa matrícula o número de empleado pertenece a una cuenta del Concurso de Catrinas que aún no está en otra inscripción de este periodo, verá tu invitación en su panel y quedará en la inscripción cuando la acepte.";
            return RedirectToAction(nameof(Editar));
        }

        [HttpPost]
        public async Task<IActionResult> CancelarInvitacion(int invitacionId)
        {
            var inscripcion = await ObtenerPropiaAsync();
            if (inscripcion == null) return RedirectToAction("Panel", "Registro");

            var invitacion = await _context.Invitaciones.FirstOrDefaultAsync(i => i.Id == invitacionId && i.InscripcionId == inscripcion.Id);
            if (invitacion != null)
            {
                _context.Invitaciones.Remove(invitacion);
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = "Invitación cancelada.";
            }
            return RedirectToAction(nameof(Editar));
        }

        [HttpPost]
        public async Task<IActionResult> AceptarInvitacion(int invitacionId)
        {
            // Solo la persona invitada puede aceptar su propia invitacion.
            var invitacion = await _context.Invitaciones
                .Include(i => i.Inscripcion).ThenInclude(ins => ins.Evaluacion)
                .FirstOrDefaultAsync(i => i.Id == invitacionId && i.RegistranteId == RegistranteId);
            if (invitacion == null || !invitacion.Inscripcion.Activo)
            {
                TempData["Error"] = "La invitación ya no está disponible.";
                return RedirectToAction("Panel", "Registro");
            }

            if (await _context.Miembros.AnyAsync(m => m.RegistranteId == RegistranteId && m.Periodo == invitacion.Periodo))
            {
                TempData["Error"] = "Ya estás en una inscripción de este periodo.";
                return RedirectToAction("Panel", "Registro");
            }

            // Mismo candado que editar la inscripcion: sin evaluacion y antes de la fecha limite de requisitos.
            var motivo = ReglasInscripcion.MotivoEdicionBloqueada(invitacion.Inscripcion, await ConfiguracionCatrinas.ObtenerAsync(_context, invitacion.Periodo));
            if (motivo != null)
            {
                TempData["Error"] = $"No puedes unirte a esta inscripción: {motivo}";
                return RedirectToAction("Panel", "Registro");
            }

            _context.Miembros.Add(new MiembroInscripcion { InscripcionId = invitacion.InscripcionId, RegistranteId = RegistranteId, Periodo = invitacion.Periodo });
            // Con inscripcion en este periodo, sus demas invitaciones del periodo ya no aplican.
            _context.Invitaciones.RemoveRange(
                await _context.Invitaciones.Where(i => i.RegistranteId == RegistranteId && i.Periodo == invitacion.Periodo).ToListAsync());
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // El indice unico (RegistranteId, Periodo) gano la carrera: dos invitaciones aceptadas a la vez.
                TempData["Error"] = "Ya estás en una inscripción de este periodo.";
                return RedirectToAction("Panel", "Registro");
            }

            TempData["Mensaje"] = $"Te uniste a la inscripción '{invitacion.Inscripcion.Nombre}'.";
            return RedirectToAction("Panel", "Registro");
        }

        [HttpPost]
        public async Task<IActionResult> RechazarInvitacion(int invitacionId)
        {
            var invitacion = await _context.Invitaciones.FirstOrDefaultAsync(i => i.Id == invitacionId && i.RegistranteId == RegistranteId);
            if (invitacion != null)
            {
                _context.Invitaciones.Remove(invitacion);
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = "Invitación rechazada.";
            }
            return RedirectToAction("Panel", "Registro");
        }

        [HttpPost]
        public async Task<IActionResult> QuitarIntegrante(int registranteId)
        {
            var inscripcion = await ObtenerPropiaAsync();
            if (inscripcion == null) return RedirectToAction("Panel", "Registro");
            if (await RechazarSiBloqueadaAsync(inscripcion) is { } rechazo) return rechazo;

            if (registranteId == inscripcion.ResponsableId)
            {
                TempData["Error"] = "El responsable no puede quitarse a sí mismo de la inscripción.";
                return RedirectToAction(nameof(Editar));
            }

            var miembro = inscripcion.Miembros.FirstOrDefault(m => m.RegistranteId == registranteId);
            if (miembro != null)
            {
                _context.Miembros.Remove(miembro);
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = "Integrante quitado.";
            }
            return RedirectToAction(nameof(Editar));
        }

        // ADR §7.1 (3): exactamente una persona personifica a la Catrina.
        [HttpPost]
        public async Task<IActionResult> DesignarPersonificador(int registranteId)
        {
            var inscripcion = await ObtenerPropiaAsync();
            if (inscripcion == null) return RedirectToAction("Panel", "Registro");
            if (await RechazarSiBloqueadaAsync(inscripcion) is { } rechazo) return rechazo;

            var elegido = inscripcion.Miembros.FirstOrDefault(m => m.RegistranteId == registranteId);
            if (elegido == null)
            {
                TempData["Error"] = "Solo puedes designar a un integrante de la inscripción.";
                return RedirectToAction(nameof(Editar));
            }

            // Primero se libera el rol (el indice unico filtrado no admite dos Personificadores a la vez).
            await using var transaccion = await _context.Database.BeginTransactionAsync();
            foreach (var miembro in inscripcion.Miembros.Where(m => m.Rol == RolInscripcion.Personificador)) miembro.Rol = RolInscripcion.Integrante;
            await _context.SaveChangesAsync();
            elegido.Rol = RolInscripcion.Personificador;
            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            TempData["Mensaje"] = $"{elegido.Registrante.NombreCompleto} personificará a la Catrina.";
            return RedirectToAction(nameof(Editar));
        }

        // --- Auxiliares ---

        private Task<Inscripcion?> ObtenerPropiaAsync() =>
            _context.Inscripciones
                .Include(i => i.Carrera)
                .Include(i => i.MaestroEncargado)
                .Include(i => i.Evaluacion)
                .Include(i => i.Miembros).ThenInclude(m => m.Registrante)
                .AsSplitQuery()
                .FirstOrDefaultAsync(i => i.Activo && i.ResponsableId == RegistranteId && i.Periodo == PeriodoHelper.ObtenerPeriodoActual());

        // A1: invitaciones enviadas que aun no se aceptan. Solo la matricula que el responsable escribio, no el nombre.
        private async Task<List<(int Id, string Identificador)>> InvitacionesPendientesAsync(int inscripcionId) =>
            (await _context.Invitaciones.AsNoTracking()
                .Where(i => i.InscripcionId == inscripcionId)
                .OrderBy(i => i.CreadaEn)
                .Select(i => new { i.Id, i.Registrante.Identificador })
                .ToListAsync())
            .Select(i => (i.Id, i.Identificador)).ToList();

        private async Task<IActionResult?> RechazarSiBloqueadaAsync(Inscripcion inscripcion)
        {
            var motivo = ReglasInscripcion.MotivoEdicionBloqueada(inscripcion, await ConfiguracionCatrinas.ObtenerAsync(_context, inscripcion.Periodo));
            if (motivo == null) return null;
            TempData["Error"] = motivo;
            return RedirectToAction(nameof(Editar));
        }

        private async Task<string?> MotivoNoPuedeCrearAsync(string periodo)
        {
            var config = await ConfiguracionCatrinas.ObtenerAsync(_context, periodo);
            if (FechaConcursoHelper.LimitePasado(config.FechaLimiteInscripcion))
            {
                return $"La inscripción cerró el {FechaConcursoHelper.Formatear(config.FechaLimiteInscripcion!.Value)}.";
            }
            if (await _context.Miembros.AnyAsync(m => m.RegistranteId == RegistranteId && m.Periodo == periodo))
            {
                return "Ya perteneces a una inscripción de Catrina en este periodo.";
            }
            return null;
        }

        private async Task<(int? MaestroId, string? AltarId, string? AltarNombre)> ValidarFormularioAsync(
            InscripcionFormViewModel model, string nombre, string periodo, int? idActual)
        {
            if (!await _context.Carreras.AnyAsync(c => c.Id == model.CarreraId && c.Activo))
            {
                ModelState.AddModelError(nameof(model.CarreraId), "Selecciona una carrera válida.");
            }
            if (await _context.Inscripciones.AnyAsync(i => i.Nombre == nombre && i.Periodo == periodo && i.Id != idActual))
            {
                ModelState.AddModelError(nameof(model.Nombre), "Ya existe una inscripción con ese nombre en este periodo.");
            }

            int? maestroId = null;
            if (!string.IsNullOrWhiteSpace(model.MaestroEncargadoIdentificador))
            {
                var identificador = model.MaestroEncargadoIdentificador.Trim();
                maestroId = await _context.Registrantes
                    .Where(r => r.Identificador == identificador && r.Activo && r.Tipo != TipoRegistrante.Alumno)
                    .Select(r => (int?)r.Id).FirstOrDefaultAsync();
                if (maestroId == null)
                {
                    ModelState.AddModelError(nameof(model.MaestroEncargadoIdentificador),
                        "No hay ningún maestro o administrativo registrado en Catrinas con ese número. Debe registrarse primero (o deja el campo vacío: es opcional).");
                }
            }

            var (altarId, altarNombre, errorAltar) = ReferenciaAltar.Normalizar(model.AltarExternalId, model.AltarNombreSnapshot);
            if (errorAltar != null) ModelState.AddModelError(nameof(model.AltarExternalId), errorAltar);

            return (maestroId, altarId, altarNombre);
        }

        private async Task<InscripcionFormViewModel> ConCarrerasAsync(InscripcionFormViewModel model)
        {
            model.Carreras = await _context.Carreras.Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync();
            return model;
        }
    }
}
