using System.Security.Cryptography;
using System.Text;
using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Controllers
{
    public class AccesoController : Controller
    {
        private readonly AltarDbContext _context;

        public AccesoController(AltarDbContext context) { _context = context; }

        // La vista de login independiente se retiró: el landing de dos pestañas
        // (Jueces/Admin + Registro) vive ahora en RegistroController.Login.
        public IActionResult Login() { return RedirectToAction("Login", "Registro"); }

        [HttpPost]
        public async Task<IActionResult> Login(string usuario, string password)
        {
            var entrada = (usuario ?? string.Empty).Trim();
            var juez = await _context.Jueces
                .FirstOrDefaultAsync(j => j.Usuario == entrada || j.CorreoInstitucional == entrada.ToLower());

            if (juez == null || !VerificarHash(password, juez.Password))
            {
                return RedirectToAction("Login", "Registro", new { error = "Usuario o contraseña incorrectos", tab = "acceso" });
            }

            if (juez.Pendiente)
            {
                return RedirectToAction("Login", "Registro", new { error = "Tu solicitud aún está pendiente de aprobación por un administrador.", tab = "acceso" });
            }

            HttpContext.Session.SetInt32("JuezId", juez.Id); // Guardar sesión
            HttpContext.Session.SetString("JuezNombre", juez.NombreCompleto ?? juez.Usuario ?? juez.CorreoInstitucional ?? "Juez");
            HttpContext.Session.SetString("JuezRol", juez.Rol); // RBAC: guardar rol en sesión
            return RedirectToAction("Historial", "AltarEvaluacion");
        }

        internal static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        internal static bool VerificarHash(string password, string? hash)
        {
            return !string.IsNullOrEmpty(hash) && HashPassword(password) == hash;
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