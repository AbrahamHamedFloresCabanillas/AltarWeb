using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Filters
{
    // SEC-03: si el Juez/Admin logueado tiene DebeCambiarPassword=true (cuenta admin semilla u otra
    // marcada explicitamente), bloquea cualquier accion que no sea el propio cambio de contraseña o el
    // logout, redirigiendo siempre a /Acceso/CambiarPasswordObligatorio. Registrado como filtro global en
    // Program.cs (AddControllersWithViews); se resuelve por request via DI, por lo que puede inyectar el
    // AltarDbContext (scoped) sin problema.
    public class ForzarCambioPasswordFilter : IAsyncActionFilter
    {
        private readonly AltarDbContext _context;

        public ForzarCambioPasswordFilter(AltarDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var juezId = context.HttpContext.Session.GetInt32("JuezId");
            if (juezId != null
                && context.ActionDescriptor is ControllerActionDescriptor descriptor
                && !EsAccionExenta(descriptor.ControllerName, descriptor.ActionName))
            {
                var debeCambiar = await _context.Jueces.AsNoTracking()
                    .Where(j => j.Id == juezId)
                    .Select(j => (bool?)j.DebeCambiarPassword)
                    .FirstOrDefaultAsync();

                if (debeCambiar == true)
                {
                    context.Result = new Microsoft.AspNetCore.Mvc.RedirectToActionResult("CambiarPasswordObligatorio", "Acceso", null);
                    return;
                }
            }

            await next();
        }

        private static bool EsAccionExenta(string controller, string action) =>
            controller == "Acceso" && (action == "CambiarPasswordObligatorio" || action == "Salir" || action == "Login");
    }
}
