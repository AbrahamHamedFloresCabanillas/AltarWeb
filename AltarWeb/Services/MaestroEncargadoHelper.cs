using AltarWeb.Models;
using AltarWeb.Models.Registro;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Services
{
    // Resuelve el identificador de maestro capturado en la Ficha de Registro: si ya existe un
    // Registrante con ese identificador (Maestro o Administrativo) lo vincula directo y lo promueve
    // a Maestro (regla explicita del comite); si no existe todavia, lo deja pendiente hasta que se
    // registre (RegistroController lo vincula en el signup).
    //
    // Vivia como metodo privado de RegistroController; se saco aqui porque la edicion
    // administrativa de equipos (AltarAdminController.EditarEquipo) necesita exactamente la misma
    // regla y no debe tener una copia que pueda divergir.
    public static class MaestroEncargadoHelper
    {
        public static async Task<(int? MaestroId, string? Pendiente, string? Error)> ResolverAsync(AltarDbContext context, string? identificadorCrudo)
        {
            var identificador = (identificadorCrudo ?? string.Empty).Trim();

            if (identificador.Length != 5 || !identificador.All(char.IsDigit))
            {
                return (null, null, "La matrícula o número de empleado del maestro encargado debe tener exactamente 5 dígitos.");
            }

            var candidato = await context.Registrantes.FirstOrDefaultAsync(r => r.Identificador == identificador);
            if (candidato == null)
            {
                return (null, identificador, null);
            }

            if (candidato.Tipo == TipoRegistrante.Alumno)
            {
                return (null, null, "Ese identificador pertenece a un alumno; el maestro encargado debe ser maestro o administrativo.");
            }

            if (candidato.Tipo == TipoRegistrante.Administrativo)
            {
                candidato.Tipo = TipoRegistrante.Maestro;
            }

            return (candidato.Id, null, null);
        }
    }
}
