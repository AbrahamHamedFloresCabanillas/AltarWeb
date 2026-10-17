using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Services
{
    // Revision de seguridad F3 (M2), portada desde Catrinas: antes de cada accion revalida contra la BD la
    // cuenta guardada en la sesion. Si el juez fue dado de baja (soft delete) o quedo pendiente, o si el
    // participante fue desactivado, la sesion se limpia y los chequeos existentes de cada controlador
    // (EstaAutenticado/EsAdmin/ObtenerRegistranteSesionAsync) lo mandan al acceso. Tambien refresca el rol
    // del juez, para que quitar el rol de Admin surta efecto sin esperar a que expire la sesion.
    // No sustituye la autorizacion manual (SEC-08); solo evita que una sesion revocada siga operando.
    public class RevalidarSesionFilter : IAsyncActionFilter
    {
        private readonly AltarDbContext _context;

        public RevalidarSesionFilter(AltarDbContext context) => _context = context;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var sesion = context.HttpContext.Session;

            var juezId = sesion.GetInt32(ClavesSesion.JuezId);
            if (juezId != null)
            {
                // El filtro global de Juez ya excluye los dados de baja (IsDeleted).
                var rol = await _context.Jueces.AsNoTracking()
                    .Where(j => j.Id == juezId && !j.Pendiente)
                    .Select(j => j.Rol)
                    .FirstOrDefaultAsync();
                if (rol == null) sesion.Clear();
                else sesion.SetString("JuezRol", rol);
            }

            var registranteId = sesion.GetInt32("RegistranteId");
            if (registranteId != null && !await _context.Registrantes.AsNoTracking().AnyAsync(r => r.Id == registranteId && r.Activo))
            {
                sesion.Clear();
            }

            await next();
        }
    }
}
