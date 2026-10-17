using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Services
{
    // Implementacion de Altares para el filtro transversal ForzarCambioPasswordFilter (SEC-03, Core):
    // consulta Juez.DebeCambiarPassword en la base de esta aplicacion.
    public class VerificadorCambioPasswordJuez : IVerificadorCambioPassword
    {
        private readonly AltarDbContext _context;

        public VerificadorCambioPasswordJuez(AltarDbContext context)
        {
            _context = context;
        }

        public async Task<bool> DebeCambiarPasswordAsync(int juezId)
        {
            var debeCambiar = await _context.Jueces.AsNoTracking()
                .Where(j => j.Id == juezId)
                .Select(j => (bool?)j.DebeCambiarPassword)
                .FirstOrDefaultAsync();

            return debeCambiar == true;
        }
    }
}
