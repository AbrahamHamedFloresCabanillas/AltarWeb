using Microsoft.EntityFrameworkCore;

namespace Concurso.Core.Datos
{
    // Serializa una seccion critica entre peticiones (y entre instancias de la app) con un bloqueo de
    // aplicacion de SQL Server ligado a una transaccion. Se usa para recalcular los lugares de una
    // carrera: sin esto, dos jueces cerrando Final a la vez leian cada uno su propia foto y el ultimo
    // en guardar podia dejar lugares desactualizados (Altares) o lanzar un conflicto de rowversion que
    // terminaba en 500 (Catrinas). Hallazgo de la prueba de carga de F4.
    //
    // Fuera de SQL Server (p. ej. el proveedor InMemory de las pruebas unitarias) ejecuta la accion sin
    // bloqueo: ahi no hay concurrencia real que proteger.
    public static class BloqueoExclusivo
    {
        public static async Task EjecutarAsync(DbContext context, string recurso, Func<Task> accion, int esperaMaximaMs = 15000)
        {
            if (!context.Database.IsRelational() || context.Database.CurrentTransaction != null)
            {
                await accion();
                return;
            }

            await using var transaccion = await context.Database.BeginTransactionAsync();
            // sp_getapplock devuelve < 0 si no obtuvo el bloqueo (tiempo agotado, interbloqueo...).
            var resultado = await context.Database
                .SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {recurso}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {esperaMaximaMs}; SELECT @r AS [Value]")
                .ToListAsync();
            if (resultado.Count == 0 || resultado[0] < 0)
            {
                throw new TimeoutException($"No se obtuvo el bloqueo '{recurso}' (resultado {(resultado.Count == 0 ? "vacio" : resultado[0])}).");
            }

            await accion();
            await transaccion.CommitAsync();
        }
    }
}
