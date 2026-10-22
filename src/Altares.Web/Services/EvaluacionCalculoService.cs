using AltarWeb.Models;
using AltarWeb.Models.Registro;
using Microsoft.EntityFrameworkCore;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace AltarWeb.Services
{
    // Formulas de la seccion 7.7 y 7.2 de vision.md, con pesos/escala tomados de
    // ConfiguracionPeriodo (editables desde /Admin sin tocar codigo).
    public class EvaluacionCalculoService
    {
        private readonly AltarDbContext _context;

        public EvaluacionCalculoService(AltarDbContext context)
        {
            _context = context;
        }

        public decimal CalcularPuntajeElementos(IEnumerable<ElementoEvaluado> elementosEvaluados, ConfiguracionPeriodo config)
        {
            decimal sumaPonderada = 0;
            decimal sumaMaxima = 0;

            foreach (var ee in elementosEvaluados)
            {
                var peso = ee.Elemento.Categoria == CategoriaElemento.RitualObligatorio
                    ? config.PesoElementoRitual
                    : config.PesoElementoDecorativo;

                var valor = ValorDeSatisfaccion(ee.Satisfaccion, config);

                sumaPonderada += valor * peso;
                sumaMaxima += peso * config.ValorSatisfaccionBueno;
            }

            if (sumaMaxima == 0) return 0;
            return Math.Round((sumaPonderada / sumaMaxima) * 10, 2);
        }

        public static decimal ValorDeSatisfaccion(Satisfaccion satisfaccion, ConfiguracionPeriodo config) => satisfaccion switch
        {
            Satisfaccion.NoLoTiene => config.ValorSatisfaccionNoLoTiene,
            Satisfaccion.Regular => config.ValorSatisfaccionRegular,
            Satisfaccion.Bueno => config.ValorSatisfaccionBueno,
            _ => 0
        };

        // Bonus tematico (equivalente al BonusTematicos del sistema anterior): cada elemento
        // marcado "tematizado" suma BonusPorElementoTematizado a EsenciaPersonalidad (etiqueta "Creatividad" en la UI),
        // tope 10. Se calcula aparte de EsenciaPersonalidad (nota cruda del juez) para que no se
        // acumule de mas si la evaluacion se guarda varias veces en Preliminar.
        public decimal CalcularEsenciaPersonalidadFinal(decimal esenciaPersonalidadJuez, int cantidadTematizados, ConfiguracionPeriodo config)
        {
            var bonus = cantidadTematizados * config.BonusPorElementoTematizado;
            return Math.Min(10, Math.Round(esenciaPersonalidadJuez + bonus, 2));
        }

        public decimal CalcularNotaFinal(Evaluacion eval, ConfiguracionPeriodo config)
        {
            var esenciaPersonalidad = eval.EsenciaPersonalidadFinal ?? eval.EsenciaPersonalidad;

            var nota = eval.ObjetivoCultural * config.PesoObjetivoCultural
                + esenciaPersonalidad * config.PesoEsenciaPersonalidad
                + eval.ValoracionGeneral * config.PesoValoracionGeneral
                + eval.DistribucionNiveles * config.PesoDistribucionNiveles
                + eval.Narrador * config.PesoNarrador;

            return Math.Round(nota, 2);
        }

        // Lugar (1/2/3) por Carrera+Periodo, solo entre evaluaciones en estado Final (seccion 13.4).
        // Desde F2 (2026-10-17) Altares ya no calcula el ranking de Catrina: el concurso de Catrinas
        // vive en Catrinas.Web. EvaluacionCatrina.LugarCatrina queda como dato historico sin uso.
        //
        // Serializado por carrera+periodo (F4): sin el bloqueo, dos cierres Final simultaneos leian cada uno
        // su propia foto y el ultimo en guardar podia dejar lugares desactualizados.
        public Task RecalcularLugaresAsync(int carreraId, string periodo) =>
            Concurso.Core.Datos.BloqueoExclusivo.EjecutarAsync(_context, $"altares-lugares:{periodo}:{carreraId}", async () =>
            {
                var finales = await _context.EvaluacionesConcurso
                    .Where(e => e.Estado == EstadoEvaluacion.Final && e.Periodo == periodo && e.Equipo.CarreraId == carreraId)
                    .OrderByDescending(e => e.NotaFinal)
                    .ToListAsync();

                foreach (var evaluacion in finales)
                {
                    // Lugar de competencia: 1 + cuantos quedan estrictamente por delante (empates: 1, 1, 3).
                    var lugar = 1 + finales.Count(otra => Compara(otra, evaluacion) > 0);
                    evaluacion.Lugar = lugar <= 3 ? lugar : null;
                }

                await _context.SaveChangesAsync();
            });

        // C4 (pruebas adversariales 2026-10-21): antes los empates recibian lugares distintos segun el orden en que
        // los devolvia la base, y ese lugar se imprime en las constancias. Decision del propietario (hibrido):
        //  1. desempata la Creatividad (EsenciaPersonalidad con su bonus). Es un criterio PROVISIONAL mientras el
        //     comite ratifica la regla (vision.md §13.11); para cambiarlo basta tocar CriterioDesempate;
        //  2. si persiste el empate, comparten lugar y TieneEmpateSinResolver lo marca en pantalla para el comite.
        private static decimal CriterioDesempate(Evaluacion e) => e.EsenciaPersonalidadFinal ?? e.EsenciaPersonalidad;

        // > 0 si 'a' va por delante de 'b'.
        private static int Compara(Evaluacion a, Evaluacion b)
        {
            var porNota = (a.NotaFinal ?? 0).CompareTo(b.NotaFinal ?? 0);
            return porNota != 0 ? porNota : CriterioDesempate(a).CompareTo(CriterioDesempate(b));
        }

        // True si la evaluacion comparte su lugar (1º a 3º) con otra Final de la misma carrera y periodo, es decir,
        // si el criterio de desempate no alcanzo y le toca resolver al comite.
        public static bool TieneEmpateSinResolver(Evaluacion evaluacion, IEnumerable<Evaluacion> finalesDeLaCarrera) =>
            evaluacion.Lugar != null
            && finalesDeLaCarrera.Any(otra => otra.Id != evaluacion.Id && otra.Lugar == evaluacion.Lugar);
    }
}
