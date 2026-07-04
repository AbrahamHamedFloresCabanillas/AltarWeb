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
                sumaMaxima += peso * config.ValorSatisfaccionMuySatisfactorio;
            }

            if (sumaMaxima == 0) return 0;
            return Math.Round((sumaPonderada / sumaMaxima) * 10, 2);
        }

        public static decimal ValorDeSatisfaccion(Satisfaccion satisfaccion, ConfiguracionPeriodo config) => satisfaccion switch
        {
            Satisfaccion.NoPresente => config.ValorSatisfaccionNoPresente,
            Satisfaccion.Poco => config.ValorSatisfaccionPoco,
            Satisfaccion.Satisfactorio => config.ValorSatisfaccionSatisfactorio,
            Satisfaccion.MuySatisfactorio => config.ValorSatisfaccionMuySatisfactorio,
            _ => 0
        };

        // Bonus tematico (equivalente al BonusTematicos del sistema anterior): cada elemento
        // marcado "tiene personalizacion" suma BonusPorElementoTematizado a EsenciaPersonalidad,
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

        public decimal CalcularNotaCatrina(EvaluacionCatrina catrina)
        {
            var valores = new[]
            {
                catrina.SombreroTocado, catrina.Guantes, catrina.Vestimenta,
                catrina.Zapatos, catrina.Collar, catrina.Maquillaje
            };
            return Math.Round(valores.Average(), 2);
        }

        // Lugar (1/2/3) por Carrera+Periodo, solo entre evaluaciones en estado Final (seccion 13.4).
        public async Task RecalcularLugaresAsync(int carreraId, string periodo)
        {
            var finales = await _context.EvaluacionesConcurso
                .Where(e => e.Estado == EstadoEvaluacion.Final && e.Periodo == periodo && e.Equipo.CarreraId == carreraId)
                .OrderByDescending(e => e.NotaFinal)
                .ToListAsync();

            for (int i = 0; i < finales.Count; i++)
            {
                finales[i].Lugar = i < 3 ? i + 1 : null;
            }

            var catrinas = await _context.EvaluacionesCatrina
                .Include(c => c.Evaluacion)
                .Where(c => c.Evaluacion.Estado == EstadoEvaluacion.Final
                    && c.Evaluacion.Periodo == periodo
                    && c.Evaluacion.Equipo.CarreraId == carreraId
                    && c.NotaCatrina != null)
                .OrderByDescending(c => c.NotaCatrina)
                .ToListAsync();

            for (int i = 0; i < catrinas.Count; i++)
            {
                catrinas[i].LugarCatrina = i < 3 ? i + 1 : null;
            }

            await _context.SaveChangesAsync();
        }
    }
}
