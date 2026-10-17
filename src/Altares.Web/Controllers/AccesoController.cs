using AltarWeb.Models;
using AltarWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Controllers
{
    public class AccesoController : Controller
    {
        private readonly AltarDbContext _context;
        private readonly ILogger<AccesoController> _logger;
        private readonly LimitadorIntentosLogin _limitador;

        // Ambito del contador de intentos por cuenta (SEC-04): cuentas de backoffice.
        private const string AmbitoLimite = "juez";
        private const string AmbitoCuentaResuelta = "juez-id";

        public AccesoController(AltarDbContext context, ILogger<AccesoController> logger, LimitadorIntentosLogin limitador)
        {
            _context = context;
            _logger = logger;
            _limitador = limitador;
        }

        // La vista de login independiente se retiró: el landing de dos pestañas
        // (Jueces/Admin + Registro) vive ahora en RegistroController.Login.
        public IActionResult Login() { return RedirectToAction("Login", "Registro"); }

        [HttpPost]
        [EnableRateLimiting(PoliticasRateLimit.LoginIp)]
        public async Task<IActionResult> Login(string usuario, string password)
        {
            var entrada = (usuario ?? string.Empty).Trim();

            // SEC-04: la cuenta intentada esta en pausa por demasiados intentos fallidos, venga de la IP que venga.
            if (_limitador.EstaBloqueada(AmbitoLimite, entrada, out var espera))
            {
                _logger.LogWarning("Login de Juez/Admin rechazado por limite de cuenta. Usuario intentado: '{Usuario}', IP: {IP}, reintentar en {Segundos}s",
                    _limitador.NormalizarCuenta(entrada), HttpContext.Connection.RemoteIpAddress, (int)Math.Ceiling(espera.TotalSeconds));
                return new ResultadoLimiteExcedido(espera);
            }

            var juez = await _context.Jueces
                .FirstOrDefaultAsync(j => j.Usuario == entrada || j.CorreoInstitucional == entrada.ToLower());

            // Una misma cuenta de juez se puede intentar por usuario o por correo: ademas del texto
            // intentado, el contador se lleva por la cuenta resuelta para que alternar entre ambos no
            // duplique los intentos permitidos.
            var cuentaResuelta = juez?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (cuentaResuelta != null && _limitador.EstaBloqueada(AmbitoCuentaResuelta, cuentaResuelta, out espera))
            {
                _logger.LogWarning("Login de Juez/Admin rechazado por limite de cuenta. JuezId: {JuezId}, IP: {IP}, reintentar en {Segundos}s",
                    juez!.Id, HttpContext.Connection.RemoteIpAddress, (int)Math.Ceiling(espera.TotalSeconds));
                return new ResultadoLimiteExcedido(espera);
            }

            password ??= string.Empty;
            var (esValido, requiereRehash) = PasswordHashService.Verificar(password, juez?.Password);
            if (juez == null || !esValido)
            {
                _limitador.RegistrarFallo(AmbitoLimite, entrada);
                if (cuentaResuelta != null) _limitador.RegistrarFallo(AmbitoCuentaResuelta, cuentaResuelta);
                // LOG-01: registra el intento fallido (usuario intentado + IP), nunca la contraseña.
                _logger.LogWarning("Login fallido de Juez/Admin. Usuario intentado: '{Usuario}', IP: {IP}",
                    entrada, HttpContext.Connection.RemoteIpAddress);
                return RedirectToAction("Login", "Registro", new { error = "Usuario o contraseña incorrectos", tab = "acceso" });
            }

            _limitador.Reiniciar(AmbitoLimite, entrada);
            _limitador.Reiniciar(AmbitoCuentaResuelta, cuentaResuelta);

            if (juez.Pendiente)
            {
                return RedirectToAction("Login", "Registro", new { error = "Tu solicitud aún está pendiente de aprobación por un administrador.", tab = "acceso" });
            }

            if (requiereRehash)
            {
                juez.Password = PasswordHashService.HashPassword(password);
                await _context.SaveChangesAsync();
            }

            HttpContext.Session.SetInt32("JuezId", juez.Id); // Guardar sesión
            HttpContext.Session.SetString("JuezNombre", juez.NombreCompleto ?? juez.Usuario ?? juez.CorreoInstitucional ?? "Juez");
            HttpContext.Session.SetString("JuezRol", juez.Rol); // RBAC: guardar rol en sesión

            if (juez.DebeCambiarPassword)
            {
                return RedirectToAction("CambiarPasswordObligatorio");
            }

            return RedirectToAction("Historial", "AltarEvaluacion");
        }

        internal static string HashPassword(string password) => PasswordHashService.HashPassword(password);

        // SEC-03: fuerza el cambio de contraseña (cuenta admin semilla u otras marcadas) antes de
        // permitir cualquier otra acción. El filtro global ForzarCambioPasswordFilter redirige aquí.
        public IActionResult CambiarPasswordObligatorio()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Registro");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CambiarPasswordObligatorio(string nuevaPassword, string confirmarPassword)
        {
            var juezId = HttpContext.Session.GetInt32("JuezId");
            if (juezId == null) return RedirectToAction("Login", "Registro");

            if (string.IsNullOrWhiteSpace(nuevaPassword) || nuevaPassword.Length < 8)
            {
                ModelState.AddModelError(string.Empty, "La nueva contraseña debe tener al menos 8 caracteres.");
            }
            else if (nuevaPassword != confirmarPassword)
            {
                ModelState.AddModelError(string.Empty, "Las contraseñas no coinciden.");
            }

            if (!ModelState.IsValid) return View();

            var juez = await _context.Jueces.FirstOrDefaultAsync(j => j.Id == juezId);
            if (juez == null) return RedirectToAction("Login", "Registro");

            juez.Password = PasswordHashService.HashPassword(nuevaPassword);
            juez.DebeCambiarPassword = false;
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Contraseña actualizada.";
            return RedirectToAction("Historial", "AltarEvaluacion");
        }

        // Nota: no hay registro público de Jueces; solo un Admin puede crearlos desde
        // /AltarAdmin/Jueces (o quedan Pendientes vía /Registro/SignupJuez hasta su aprobación).
        public IActionResult Salir()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
