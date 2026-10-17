using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Concurso.Core.Seguridad
{
    // Cada aplicacion responde, con su propia base de datos, si la cuenta de backoffice debe cambiar
    // su contrasena antes de seguir. Core no conoce la entidad Juez ni el DbContext de ninguna app.
    public interface IVerificadorCambioPassword
    {
        Task<bool> DebeCambiarPasswordAsync(int juezId);
    }

    // SEC-03: si el Juez/Admin logueado tiene DebeCambiarPassword=true (cuenta admin semilla u otra
    // marcada explicitamente), bloquea cualquier accion que no sea el propio cambio de contraseña o el
    // logout, redirigiendo siempre a /Acceso/CambiarPasswordObligatorio. Se registra como filtro global
    // (ver ConcursoSeguridadExtensions.AgregarFiltrosSeguridad); se resuelve por request via DI.
    public class ForzarCambioPasswordFilter : IAsyncActionFilter
    {
        private readonly IVerificadorCambioPassword _verificador;

        public ForzarCambioPasswordFilter(IVerificadorCambioPassword verificador)
        {
            _verificador = verificador;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var juezId = context.HttpContext.Session.GetInt32(ClavesSesion.JuezId);
            if (juezId != null
                && context.ActionDescriptor is ControllerActionDescriptor descriptor
                && !EsAccionExenta(descriptor.ControllerName, descriptor.ActionName)
                && await _verificador.DebeCambiarPasswordAsync(juezId.Value))
            {
                context.Result = new RedirectToActionResult("CambiarPasswordObligatorio", "Acceso", null);
                return;
            }

            await next();
        }

        private static bool EsAccionExenta(string controller, string action) =>
            controller == "Acceso" && (action == "CambiarPasswordObligatorio" || action == "Salir" || action == "Login");
    }
}
