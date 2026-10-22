using System.Globalization;
using Catrinas.Web.Datos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Web.Controllers
{
    // Acceso del jurado y la administracion de Catrinas (cuentas propias, independientes de Altares).
    public class AccesoController : Controller
    {
        internal const string AmbitoLimite = "jurado";
        internal const string AmbitoCuentaResuelta = "jurado-id";
        private readonly CatrinasDbContext _context;
        private readonly ILogger<AccesoController> _logger;
        private readonly LimitadorIntentosLogin _limitador;

        public AccesoController(CatrinasDbContext context, ILogger<AccesoController> logger, LimitadorIntentosLogin limitador)
        {
            _context = context;
            _logger = logger;
            _limitador = limitador;
        }

        [AccesoPublico]
        public IActionResult Login() => RedirectToAction("Login", "Registro", new { tab = "jurado" });

        [AccesoPublico]
        [HttpPost]
        [EnableRateLimiting(PoliticasRateLimit.LoginIp)]
        public async Task<IActionResult> Login(string usuario, string password)
        {
            var entrada = (usuario ?? string.Empty).Trim();
            if (_limitador.EstaBloqueada(AmbitoLimite, entrada, out var espera))
            {
                _logger.LogWarning("Login del jurado rechazado por límite de cuenta. Usuario: '{Usuario}', IP: {IP}",
                    _limitador.NormalizarCuenta(entrada), HttpContext.Connection.RemoteIpAddress);
                return new ResultadoLimiteExcedido(espera);
            }

            var correo = entrada.ToLowerInvariant();
            var juez = await _context.Jueces.FirstOrDefaultAsync(j => j.Usuario == entrada || j.CorreoInstitucional == correo);
            var cuentaResuelta = juez?.Id.ToString(CultureInfo.InvariantCulture);
            if (cuentaResuelta != null && _limitador.EstaBloqueada(AmbitoCuentaResuelta, cuentaResuelta, out espera))
            {
                return new ResultadoLimiteExcedido(espera);
            }

            var (valido, requiereRehash) = PasswordHashService.Verificar(password ?? string.Empty, juez?.Password);
            if (juez == null || !valido || juez.ProveedorAuth == "Google")
            {
                _limitador.RegistrarFallo(AmbitoLimite, entrada);
                if (cuentaResuelta != null) _limitador.RegistrarFallo(AmbitoCuentaResuelta, cuentaResuelta);
                _logger.LogWarning("Login fallido del jurado. Usuario intentado: '{Usuario}', IP: {IP}", entrada, HttpContext.Connection.RemoteIpAddress);
                return RegistroController.RedirigirALogin(this, "Usuario o contraseña incorrectos.", "jurado");
            }

            _limitador.Reiniciar(AmbitoLimite, entrada);
            _limitador.Reiniciar(AmbitoCuentaResuelta, cuentaResuelta);

            if (!juez.Activo) return RegistroController.RedirigirALogin(this, "Tu cuenta del jurado fue desactivada.", "jurado");
            if (juez.Pendiente) return RegistroController.RedirigirALogin(this, "Tu solicitud aún está pendiente de aprobación.", "jurado");

            if (requiereRehash)
            {
                juez.Password = PasswordHashService.HashPassword(password!);
                await _context.SaveChangesAsync();
            }

            HttpContext.Session.IniciarJuez(juez);
            return juez.DebeCambiarPassword ? RedirectToAction(nameof(CambiarPasswordObligatorio)) : RedirectToAction("Index", "Evaluacion");
        }

        [SoloJurado]
        public IActionResult CambiarPasswordObligatorio() => View();

        [SoloJurado]
        [HttpPost]
        public async Task<IActionResult> CambiarPasswordObligatorio(string nuevaPassword, string confirmarPassword)
        {
            if (string.IsNullOrWhiteSpace(nuevaPassword) || nuevaPassword.Length < 8)
            {
                ModelState.AddModelError(string.Empty, "La nueva contraseña debe tener al menos 8 caracteres.");
            }
            else if (nuevaPassword != confirmarPassword)
            {
                ModelState.AddModelError(string.Empty, "Las contraseñas no coinciden.");
            }
            if (!ModelState.IsValid) return View();

            var juez = await _context.Jueces.FirstOrDefaultAsync(j => j.Id == HttpContext.Session.JuezId());
            if (juez == null) return RedirectToAction("Login", "Registro", new { tab = "jurado" });

            juez.Password = PasswordHashService.HashPassword(nuevaPassword);
            juez.DebeCambiarPassword = false;
            await _context.SaveChangesAsync();
            TempData["Mensaje"] = "Contraseña actualizada.";
            return RedirectToAction("Index", "Evaluacion");
        }

        [AccesoPublico]
        public IActionResult Salir()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Registro", new { tab = "jurado" });
        }
    }
}
