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

        public AccesoController(AltarDbContext context, ILogger<AccesoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // La vista de login independiente se retiró: el landing de dos pestañas
        // (Jueces/Admin + Registro) vive ahora en RegistroController.Login.
        public IActionResult Login() { return RedirectToAction("Login", "Registro"); }

        [HttpPost]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(string usuario, string password)
        {
            var entrada = (usuario ?? string.Empty).Trim();
            var juez = await _context.Jueces
                .FirstOrDefaultAsync(j => j.Usuario == entrada || j.CorreoInstitucional == entrada.ToLower());

            password ??= string.Empty;
            var (esValido, requiereRehash) = PasswordHashService.Verificar(password, juez?.Password);
            if (juez == null || !esValido)
            {
                // LOG-01: registra el intento fallido (usuario intentado + IP), nunca la contraseña.
                _logger.LogWarning("Login fallido de Juez/Admin. Usuario intentado: '{Usuario}', IP: {IP}",
                    entrada, HttpContext.Connection.RemoteIpAddress);
                return RedirectToAction("Login", "Registro", new { error = "Usuario o contraseña incorrectos", tab = "acceso" });
            }

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

        // Registro público deshabilitado — solo Admins pueden crear Jueces desde /AltarAdmin/Jueces
        public IActionResult Registrar()
        {
            return RedirectToAction("Login");
        }

        public IActionResult Salir()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
