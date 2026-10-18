using System.Text.RegularExpressions;
using Catrinas.Web.Datos;
using Catrinas.Web.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Web
{
    // Referencia blanda a un equipo de Altares (ADR §2.6). Solo valida formato y longitud: NUNCA consulta
    // a Altares (ni su base ni sus endpoints), asi que funciona aunque Altares no exista.
    public static class ReferenciaAltar
    {
        public const int LongitudMaximaId = 32;
        public const int LongitudMaximaNombre = 150;
        private static readonly Regex Formato = new(@"^ALT-\d{4}-[12]-\d{4,10}$", RegexOptions.CultureInvariant);

        // Devuelve (id normalizado, nombre normalizado, error). Ambos vacios = sin referencia.
        public static (string? Id, string? Nombre, string? Error) Normalizar(string? id, string? nombre)
        {
            var idLimpio = string.IsNullOrWhiteSpace(id) ? null : id.Trim().ToUpperInvariant();
            var nombreLimpio = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();

            if (idLimpio == null)
            {
                return nombreLimpio == null
                    ? (null, null, null)
                    : (null, null, "Si indicas el nombre del equipo de Altares, captura también su código (ALT-AAAA-P-NNNN).");
            }

            if (idLimpio.Length > LongitudMaximaId || !Formato.IsMatch(idLimpio))
            {
                return (null, null, "El código del equipo de Altares debe tener el formato ALT-AAAA-P-NNNN (por ejemplo ALT-2026-2-0042).");
            }

            if (nombreLimpio != null && nombreLimpio.Length > LongitudMaximaNombre)
            {
                return (null, null, $"El nombre del equipo de Altares admite como máximo {LongitudMaximaNombre} caracteres.");
            }

            return (idLimpio, nombreLimpio, null);
        }
    }

    // Requisitos de una inscripcion de Catrina (ADR §7.1). Son reglas propias de Catrinas.
    public static class ReglasInscripcion
    {
        public static int ContarPersonificadores(Inscripcion inscripcion) =>
            inscripcion.Miembros.Count(m => m.Rol == RolInscripcion.Personificador);

        // Lo que falta para poder cerrar la evaluacion como Final: nombre, carrera y exactamente un
        // Personificador. El maestro encargado es opcional y no se exige.
        public static IReadOnlyList<string> Faltantes(Inscripcion inscripcion)
        {
            var faltantes = new List<string>();
            if (string.IsNullOrWhiteSpace(inscripcion.Nombre)) faltantes.Add("nombre de la inscripción");
            if (inscripcion.CarreraId <= 0) faltantes.Add("carrera");
            if (ContarPersonificadores(inscripcion) != 1) faltantes.Add("persona que personifica a la Catrina");
            return faltantes;
        }

        public static bool EstaListaParaFinal(Inscripcion inscripcion, out string? motivo)
        {
            var faltantes = Faltantes(inscripcion);
            motivo = faltantes.Count == 0 ? null : $"falta: {string.Join(", ", faltantes)}";
            return faltantes.Count == 0;
        }

        // Con evaluacion registrada (Preliminar o Final) o pasada la fecha limite de requisitos, la
        // inscripcion y su lista de integrantes quedan cerradas para el participante.
        public static string? MotivoEdicionBloqueada(Inscripcion inscripcion, ConfiguracionPeriodo config)
        {
            if (inscripcion.Evaluacion != null) return "La inscripción ya tiene una evaluación del jurado; ya no puede modificarse.";
            if (FechaConcursoHelper.LimitePasado(config.FechaLimiteRequisitos))
            {
                return $"La fecha límite de requisitos ({FechaConcursoHelper.Formatear(config.FechaLimiteRequisitos!.Value)}) ya pasó.";
            }
            return null;
        }
    }

    public static class ConfiguracionCatrinas
    {
        // Configuracion del periodo, o los valores por defecto si el admin aun no la guardo.
        public static async Task<ConfiguracionPeriodo> ObtenerAsync(CatrinasDbContext context, string periodo) =>
            await context.Configuraciones.AsNoTracking().FirstOrDefaultAsync(c => c.Periodo == periodo)
            ?? new ConfiguracionPeriodo { Periodo = periodo };
    }

    // Nota ponderada y ranking por carrera (ADR §7.1, vision.md §7.5).
    public static class CalculoCatrina
    {
        public static decimal CalcularNota(decimal vestuario, decimal maquillaje, decimal accesorios, decimal originalidad, decimal apego,
            ConfiguracionPeriodo pesos) =>
            Math.Round(vestuario * pesos.PesoVestuario
                + maquillaje * pesos.PesoMaquillaje
                + accesorios * pesos.PesoAccesorios
                + originalidad * pesos.PesoOriginalidadCreatividad
                + apego * pesos.PesoApegoTradiciones, 2);

        public static bool PesosValidos(ConfiguracionPeriodo pesos) =>
            pesos.SumaPesos == 1m
            && new[] { pesos.PesoVestuario, pesos.PesoMaquillaje, pesos.PesoAccesorios, pesos.PesoOriginalidadCreatividad, pesos.PesoApegoTradiciones }
                .All(p => p >= 0m);

        // Lugar por carrera y periodo entre evaluaciones Final, por NotaCatrina descendente.
        // Regla de empates: NO hay criterio de desempate definido por el comite (vision.md §13.11), asi que
        // NO se inventa uno (ni fecha, ni Id, ni otro campo incidental): notas iguales comparten lugar y el
        // siguiente lugar se salta (ranking de competencia: 1, 1, 3). Solo los lugares 1 a 3 se registran.
        public static IReadOnlyDictionary<int, int?> CalcularLugares(IEnumerable<(int EvaluacionId, decimal Nota)> finales)
        {
            var lista = finales.ToList();
            return lista.ToDictionary(
                e => e.EvaluacionId,
                e =>
                {
                    var lugar = 1 + lista.Count(o => o.Nota > e.Nota);
                    return lugar <= 3 ? lugar : (int?)null;
                });
        }

        // Serializado por carrera+periodo (F4): dos cierres Final simultaneos ya no chocan por rowversion
        // ni dejan lugares calculados con una foto vieja. Se lee sin seguimiento y se escribe con
        // ExecuteUpdate porque la evaluacion recien guardada sigue rastreada en este contexto con un
        // rowversion que otro recalculo pudo haber cambiado mientras se esperaba el bloqueo.
        public static Task RecalcularLugaresAsync(CatrinasDbContext context, int carreraId, string periodo) =>
            Concurso.Core.Datos.BloqueoExclusivo.EjecutarAsync(context, $"catrinas-lugares:{periodo}:{carreraId}", async () =>
            {
                var finales = await context.Evaluaciones.AsNoTracking()
                    .Where(e => e.Estado == EstadoEvaluacion.Final && e.Periodo == periodo && e.Inscripcion.CarreraId == carreraId)
                    .Select(e => new { e.Id, e.NotaCatrina })
                    .ToListAsync();

                var lugares = CalcularLugares(finales.Select(e => (e.Id, e.NotaCatrina)));
                foreach (var grupo in lugares.GroupBy(l => l.Value))
                {
                    var ids = grupo.Select(l => l.Key).ToList();
                    var lugar = grupo.Key;
                    await context.Evaluaciones.Where(e => ids.Contains(e.Id)).ExecuteUpdateAsync(s => s.SetProperty(e => e.Lugar, lugar));
                }
            });
    }
}
