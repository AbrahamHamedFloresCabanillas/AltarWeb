using Catrinas.Web.Datos;
using Catrinas.Web.Modelos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Web
{
    public static class Sesion
    {
        public const string JuezRol = "JuezRol";
        public const string JuezNombre = "JuezNombre";
        public const string RegistranteId = "RegistranteId";

        public static int? JuezId(this ISession s) => s.GetInt32(ClavesSesion.JuezId);
        public static bool EsAdmin(this ISession s) => s.JuezId() != null && s.GetString(JuezRol) == RolesJuez.Admin;
        public static int? Registrante(this ISession s) => s.GetInt32(RegistranteId);

        public static void IniciarJuez(this ISession s, Juez juez)
        {
            s.Clear();
            s.SetInt32(ClavesSesion.JuezId, juez.Id);
            s.SetString(JuezRol, juez.Rol);
            s.SetString(JuezNombre, juez.NombreCompleto);
        }

        public static void IniciarRegistrante(this ISession s, Registrante registrante)
        {
            s.Clear();
            s.SetInt32(RegistranteId, registrante.Id);
        }
    }

    // Autorizacion por convencion (mitigacion de SEC-08): cada accion declara quien puede usarla y una
    // prueba recorre todos los controladores para verificar que ninguna accion quede sin declarar.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AccesoPublicoAttribute : Attribute { }

    // Cada peticion del jurado o de un participante revalida la cuenta contra la BD: si la administracion
    // la desactiva (o deja pendiente, o le quita el rol de admin), la sesion abierta deja de servir en la
    // siguiente peticion, en vez de seguir viva hasta que expire (revision de seguridad F3, M2).
    internal static class RevalidacionSesion
    {
        public static async Task<Juez?> JuezVigenteAsync(HttpContext http)
        {
            var id = http.Session.JuezId();
            if (id == null) return null;
            var juez = await http.RequestServices.GetRequiredService<CatrinasDbContext>().Jueces.AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == id && j.Activo && !j.Pendiente);
            if (juez == null) http.Session.Clear();
            else http.Session.SetString(Sesion.JuezRol, juez.Rol);
            return juez;
        }

        public static async Task<bool> ParticipanteVigenteAsync(HttpContext http)
        {
            var id = http.Session.Registrante();
            if (id == null) return false;
            var activo = await http.RequestServices.GetRequiredService<CatrinasDbContext>().Registrantes.AsNoTracking()
                .AnyAsync(r => r.Id == id && r.Activo);
            if (!activo) http.Session.Clear();
            return activo;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SoloJuradoAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (await RevalidacionSesion.JuezVigenteAsync(context.HttpContext) == null)
            {
                context.Result = new RedirectToActionResult("Login", "Registro", new { tab = "jurado" });
                return;
            }
            await next();
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SoloAdminAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var juez = await RevalidacionSesion.JuezVigenteAsync(context.HttpContext);
            if (juez == null)
            {
                context.Result = new RedirectToActionResult("Login", "Registro", new { tab = "jurado" });
                return;
            }
            if (juez.Rol != RolesJuez.Admin)
            {
                context.HttpContext.RequestServices.GetRequiredService<ILogger<SoloAdminAttribute>>()
                    .LogWarning("Acceso denegado (rol insuficiente). JuezId: {JuezId}, Ruta: {Ruta}", juez.Id, context.HttpContext.Request.Path);
                context.Result = new RedirectToActionResult("Index", "Evaluacion", null);
                return;
            }
            await next();
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SoloParticipanteAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!await RevalidacionSesion.ParticipanteVigenteAsync(context.HttpContext))
            {
                context.Result = new RedirectToActionResult("Login", "Registro", null);
                return;
            }
            await next();
        }
    }

    public class VerificadorCambioPasswordJuez : IVerificadorCambioPassword
    {
        private readonly CatrinasDbContext _context;
        public VerificadorCambioPasswordJuez(CatrinasDbContext context) => _context = context;

        public async Task<bool> DebeCambiarPasswordAsync(int juezId) =>
            await _context.Jueces.AsNoTracking().Where(j => j.Id == juezId).Select(j => j.DebeCambiarPassword).FirstOrDefaultAsync();
    }

    public static class SemillaCatrinas
    {
        // Aplica SOLO las migraciones de Catrinas y crea el admin semilla propio (SEC-03): contraseña de
        // SEED_ADMIN_PASSWORD o aleatoria impresa una vez en el log, con cambio obligatorio.
        public static void Inicializar(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CatrinasDbContext>();
            context.Database.Migrate();

            if (context.Jueces.Any()) return;

            var (usuario, password, generada) = CredencialesSemilla.Obtener(usuarioPorDefecto: "admin-catrinas");
            context.Jueces.Add(new Juez
            {
                Usuario = usuario,
                Password = PasswordHashService.HashPassword(password),
                Rol = RolesJuez.Admin,
                NombreCompleto = "Administración Catrinas",
                DebeCambiarPassword = true
            });
            context.SaveChanges();

            if (generada)
            {
                scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SemillaCatrinas").LogWarning(
                    "Cuenta administradora semilla de Catrinas creada. Usuario: '{Usuario}'. Password temporal (solo se muestra esta vez): '{Password}'. " +
                    "Debe cambiarse en el primer login.", usuario, password);
            }
        }
    }
}
