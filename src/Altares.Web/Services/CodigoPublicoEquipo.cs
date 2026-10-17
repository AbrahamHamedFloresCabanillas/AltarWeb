using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AltarWeb.Services
{
    // Genera el CodigoPublico de un equipo de Altares: "ALT-{Periodo}-{NNNN}" (p. ej. ALT-2026-2-0042),
    // con un consecutivo por periodo.
    //
    // Concurrencia: el consecutivo NO se calcula con COUNT/MAX + 1. Se incrementa de forma atomica la fila
    // del periodo en ContadoresCodigoPublico con un MERGE ... WITH (HOLDLOCK) ... OUTPUT dentro de la MISMA
    // transaccion que inserta el equipo:
    //   - dos creaciones simultaneas del mismo periodo se serializan en el bloqueo de esa fila (la segunda
    //     espera a que la primera confirme o revierta) y nunca reciben el mismo numero;
    //   - si la creacion del equipo se revierte, el incremento tambien se revierte (no quedan huecos);
    //   - HOLDLOCK cubre tambien el primer equipo del periodo (la fila aun no existe).
    // El indice unico de EquiposConcurso.CodigoPublico es el respaldo final en la BD.
    public static class CodigoPublicoEquipo
    {
        public const string Prefijo = "ALT";
        private static readonly Regex FormatoPeriodo = new(@"^\d{4}-[12]$", RegexOptions.CultureInvariant);

        public static string Formatear(string periodo, int consecutivo) =>
            $"{Prefijo}-{periodo}-{consecutivo.ToString("D4", CultureInfo.InvariantCulture)}";

        public static async Task<string> SiguienteAsync(AltarDbContext context, string periodo)
        {
            if (!FormatoPeriodo.IsMatch(periodo))
            {
                throw new ArgumentException($"Periodo con formato invalido: '{periodo}'.", nameof(periodo));
            }

            var transaccion = context.Database.CurrentTransaction
                ?? throw new InvalidOperationException("CodigoPublicoEquipo requiere la transaccion que inserta el equipo.");

            var conexion = context.Database.GetDbConnection();
            await using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion.GetDbTransaction();
            comando.CommandText = @"
MERGE ContadoresCodigoPublico WITH (HOLDLOCK) AS destino
USING (SELECT @periodo AS Periodo) AS origen
    ON destino.Periodo = origen.Periodo
WHEN MATCHED THEN
    UPDATE SET Ultimo = destino.Ultimo + 1
WHEN NOT MATCHED THEN
    INSERT (Periodo, Ultimo) VALUES (origen.Periodo, 1)
OUTPUT inserted.Ultimo;";

            var parametro = comando.CreateParameter();
            parametro.ParameterName = "@periodo";
            parametro.DbType = DbType.String;
            parametro.Size = 10;
            parametro.Value = periodo;
            comando.Parameters.Add(parametro);

            var resultado = await comando.ExecuteScalarAsync();
            return Formatear(periodo, Convert.ToInt32(resultado, CultureInfo.InvariantCulture));
        }
    }
}
