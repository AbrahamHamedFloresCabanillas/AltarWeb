using AltarWeb.Models;
using AltarWeb.ViewModels.Altar;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Equipo = AltarWeb.Models.Registro.Equipo;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;
using EstadoEvaluacion = AltarWeb.Models.Registro.EstadoEvaluacion;
using Registrante = AltarWeb.Models.Registro.Registrante;
using Satisfaccion = AltarWeb.Models.Registro.Satisfaccion;

namespace AltarWeb.Services
{
    // Agregaciones del Reporte de Cierre de Periodo (vision.md seccion 12). Todo dato de
    // Genero/AutodescripcionCultural pasa por PrivacidadReporteHelper antes de exponerse;
    // esta es la UNICA ruta de datos que aplica esa regla. Las vistas administrativas de
    // gestion individual (AltarAdminController) no usan este servicio.
    public class ReportePeriodoService
    {
        private readonly AltarDbContext _context;

        public ReportePeriodoService(AltarDbContext context)
        {
            _context = context;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<List<string>> ObtenerPeriodosDisponiblesAsync()
        {
            var deEquipos = await _context.EquiposConcurso.Select(e => e.Periodo).Distinct().ToListAsync();
            var deEvaluaciones = await _context.EvaluacionesConcurso.Select(e => e.Periodo).Distinct().ToListAsync();
            var periodos = deEquipos.Union(deEvaluaciones).Distinct().ToList();
            periodos.Sort((a, b) => string.CompareOrdinal(b, a));
            return periodos;
        }

        public async Task<ReportePeriodoViewModel> GenerarReporteAsync(string periodo)
        {
            var config = await _context.ConfiguracionesPeriodo.FirstOrDefaultAsync(c => c.Periodo == periodo);
            var umbral = config?.UmbralAgrupacionDemografica ?? 5;

            var equipos = await _context.EquiposConcurso
                .Where(e => e.Periodo == periodo)
                .Include(e => e.Carrera)
                .Include(e => e.Difunto)
                .Include(e => e.MaestroEncargado)
                .Include(e => e.Integrantes).ThenInclude(ri => ri.Registrante).ThenInclude(r => r!.CatalogoGenero)
                .Include(e => e.Integrantes).ThenInclude(ri => ri.Registrante).ThenInclude(r => r!.CatalogoCarrera)
                .Include(e => e.Evaluacion)
                .ToListAsync();

            var evaluaciones = await _context.EvaluacionesConcurso
                .Where(e => e.Periodo == periodo)
                .Include(e => e.Equipo).ThenInclude(eq => eq.Carrera)
                .Include(e => e.Juez)
                .Include(e => e.EvaluacionCatrina)
                .Include(e => e.ElementosEvaluados).ThenInclude(ee => ee.Elemento)
                .ToListAsync();

            var registrantesEnPeriodo = equipos
                .SelectMany(e => e.Integrantes.Select(ri => ri.Registrante))
                .Concat(equipos.Where(e => e.MaestroEncargado != null).Select(e => e.MaestroEncargado!))
                .Where(r => r != null)
                .DistinctBy(r => r.Id)
                .ToList();

            var vm = new ReportePeriodoViewModel
            {
                Periodo = periodo,
                UmbralAgrupacionDemografica = umbral,
                PeriodosDisponibles = await ObtenerPeriodosDisponiblesAsync(),
                ParticipacionGeneral = ConstruirParticipacionGeneral(equipos, evaluaciones, registrantesEnPeriodo),
                DistribucionAcademica = ConstruirDistribucionAcademica(equipos),
                ResultadosEvaluacion = ConstruirResultadosEvaluacion(evaluaciones),
                ParticipacionJueces = ConstruirParticipacionJueces(equipos, evaluaciones),
                EstadisticasDemograficas = ConstruirEstadisticasDemograficas(registrantesEnPeriodo, umbral)
            };

            return vm;
        }

        private static bool EsFichaCompleta(Equipo equipo)
        {
            return !string.IsNullOrWhiteSpace(equipo.NombreAltar)
                && !string.IsNullOrWhiteSpace(equipo.UbicacionAltar)
                && equipo.Difunto != null
                && !string.IsNullOrWhiteSpace(equipo.Difunto.Nombre)
                && equipo.MaestroEncargadoId != null;
        }

        private static ParticipacionGeneralViewModel ConstruirParticipacionGeneral(
            List<Equipo> equipos, List<Evaluacion> evaluaciones, List<Registrante> registrantesEnPeriodo)
        {
            var completas = equipos.Count(EsFichaCompleta);

            return new ParticipacionGeneralViewModel
            {
                TotalEquipos = equipos.Count,
                EquiposFichaCompleta = completas,
                EquiposFichaIncompleta = equipos.Count - completas,
                RegistrantesPorTipo = registrantesEnPeriodo
                    .GroupBy(r => r.Tipo.ToString())
                    .Select(g => (Tipo: g.Key, Conteo: g.Count()))
                    .OrderByDescending(g => g.Conteo)
                    .ToList(),
                EvaluacionesPreliminar = evaluaciones.Count(e => e.Estado == EstadoEvaluacion.Preliminar),
                EvaluacionesFinal = evaluaciones.Count(e => e.Estado == EstadoEvaluacion.Final),
                PromedioIntegrantesPorEquipo = equipos.Count > 0
                    ? Math.Round((decimal)equipos.Average(e => e.Integrantes.Count), 1)
                    : 0
            };
        }

        private static DistribucionAcademicaViewModel ConstruirDistribucionAcademica(List<Equipo> equipos)
        {
            var total = equipos.Count;

            List<ConteoConPorcentaje> ConPorcentaje(IEnumerable<IGrouping<string, Equipo>> grupos, int universo) =>
                grupos.Select(g => new ConteoConPorcentaje
                {
                    Etiqueta = g.Key,
                    Conteo = g.Count(),
                    Porcentaje = universo > 0 ? Math.Round(g.Count() * 100m / universo, 1) : 0
                }).OrderByDescending(c => c.Conteo).ToList();

            var evaluados = equipos.Where(e => e.Evaluacion != null).ToList();

            return new DistribucionAcademicaViewModel
            {
                EquiposPorCarrera = ConPorcentaje(equipos.GroupBy(e => e.Carrera.Nombre), total),
                EquiposPorTipoAltar = equipos
                    .GroupBy(e => e.Difunto?.TipoAltar.ToString() ?? "Sin especificar")
                    .Select(g => (Etiqueta: g.Key, Conteo: g.Count()))
                    .OrderByDescending(g => g.Conteo)
                    .ToList(),
                EquiposPorNiveles = evaluados
                    .GroupBy(e => $"{e.Evaluacion!.Niveles} niveles")
                    .Select(g => (Etiqueta: g.Key, Conteo: g.Count()))
                    .OrderByDescending(g => g.Conteo)
                    .ToList(),
                EquiposConCatrina = equipos.Count(e => e.HaceCatrina),
                EquiposSinCatrina = equipos.Count(e => !e.HaceCatrina)
                // RegistrantesAlumnosPorCarrera se calcula en el controlador junto con el
                // resto de demografia porque comparte el universo de registrantesEnPeriodo.
            };
        }

        private static ResultadosEvaluacionViewModel ConstruirResultadosEvaluacion(List<Evaluacion> evaluaciones)
        {
            var finales = evaluaciones.Where(e => e.Estado == EstadoEvaluacion.Final).ToList();
            var conNota = finales.Where(e => e.NotaFinal != null).ToList();

            NotaStats Stats(IEnumerable<Evaluacion> lista)
            {
                var l = lista.Where(e => e.NotaFinal != null).ToList();
                if (l.Count == 0) return new NotaStats { Cantidad = 0 };
                return new NotaStats
                {
                    Cantidad = l.Count,
                    Promedio = Math.Round(l.Average(e => e.NotaFinal!.Value), 2),
                    Maximo = l.Max(e => e.NotaFinal!.Value),
                    Minimo = l.Min(e => e.NotaFinal!.Value)
                };
            }

            var catrinasConNota = finales
                .Where(e => e.EvaluacionCatrina?.NotaCatrina != null)
                .Select(e => e.EvaluacionCatrina!.NotaCatrina!.Value)
                .ToList();

            var elementosFinales = finales.SelectMany(e => e.ElementosEvaluados).ToList();

            List<(string Elemento, int Conteo)> TopPorSatisfaccion(Satisfaccion satisfaccion)
            {
                var porElemento = elementosFinales
                    .Where(ee => ee.Satisfaccion == satisfaccion)
                    .GroupBy(ee => ee.Elemento.Nombre)
                    .Select(g => (Elemento: g.Key, Conteo: g.Count()))
                    .OrderByDescending(g => g.Conteo)
                    .ToList();

                if (porElemento.Count == 0) return new();
                var maximo = porElemento[0].Conteo;
                return porElemento.Where(g => g.Conteo == maximo).ToList();
            }

            return new ResultadosEvaluacionViewModel
            {
                NotaFinalGeneral = Stats(conNota),
                NotaFinalPorCarrera = conNota
                    .GroupBy(e => e.Equipo.Carrera.Nombre)
                    .Select(g => (Carrera: g.Key, Stats: Stats(g)))
                    .OrderBy(g => g.Carrera)
                    .ToList(),
                PromedioObjetivoCultural = Promedio(finales.Select(e => e.ObjetivoCultural)),
                PromedioEsenciaPersonalidad = Promedio(finales.Select(e => e.EsenciaPersonalidadFinal ?? e.EsenciaPersonalidad)),
                PromedioValoracionGeneral = Promedio(finales.Select(e => e.ValoracionGeneral)),
                PromedioDistribucionNiveles = Promedio(finales.Select(e => e.DistribucionNiveles)),
                PromedioNarrador = Promedio(finales.Select(e => e.Narrador)),
                PromedioNotaCatrina = catrinasConNota.Count > 0 ? Math.Round(catrinasConNota.Average(), 2) : null,
                EquiposConNotaCatrina = catrinasConNota.Count,
                PodiumAltarPorCarrera = finales
                    .Where(e => e.Lugar is >= 1 and <= 3)
                    .OrderBy(e => e.Equipo.Carrera.Nombre).ThenBy(e => e.Lugar)
                    .Select(e => new PodiumEntry
                    {
                        Carrera = e.Equipo.Carrera.Nombre,
                        Lugar = e.Lugar!.Value,
                        NombreEquipo = e.SnapshotNombreEquipo,
                        NombreAltar = e.SnapshotNombreAltar,
                        Nota = e.NotaFinal
                    }).ToList(),
                PodiumCatrinaPorCarrera = finales
                    .Where(e => e.EvaluacionCatrina?.LugarCatrina is >= 1 and <= 3)
                    .OrderBy(e => e.Equipo.Carrera.Nombre).ThenBy(e => e.EvaluacionCatrina!.LugarCatrina)
                    .Select(e => new PodiumEntry
                    {
                        Carrera = e.Equipo.Carrera.Nombre,
                        Lugar = e.EvaluacionCatrina!.LugarCatrina!.Value,
                        NombreEquipo = e.SnapshotNombreEquipo,
                        NombreAltar = e.SnapshotNombreAltar,
                        Nota = e.EvaluacionCatrina.NotaCatrina
                    }).ToList(),
                TopNoPresente = TopPorSatisfaccion(Satisfaccion.NoPresente),
                TopMuySatisfactorio = TopPorSatisfaccion(Satisfaccion.MuySatisfactorio)
            };
        }

        private static decimal? Promedio(IEnumerable<decimal> valores)
        {
            var l = valores.ToList();
            return l.Count > 0 ? Math.Round(l.Average(), 2) : null;
        }

        private static ParticipacionJuecesViewModel ConstruirParticipacionJueces(List<Equipo> equipos, List<Evaluacion> evaluaciones)
        {
            return new ParticipacionJuecesViewModel
            {
                TotalJuecesActivos = evaluaciones.Where(e => e.JuezId != null).Select(e => e.JuezId).Distinct().Count(),
                EvaluacionesPorJuez = evaluaciones
                    .Where(e => e.Juez != null)
                    .GroupBy(e => e.Juez!.NombreCompleto)
                    .Select(g => (Juez: g.Key, Conteo: g.Count()))
                    .OrderByDescending(g => g.Conteo)
                    .ToList(),
                TotalMaestrosEncargadosDistintos = equipos.Where(e => e.MaestroEncargadoId != null).Select(e => e.MaestroEncargadoId).Distinct().Count()
            };
        }

        private static EstadisticasDemograficasViewModel ConstruirEstadisticasDemograficas(List<Registrante> registrantesEnPeriodo, int umbral)
        {
            var genero = registrantesEnPeriodo
                .GroupBy(r => r.CatalogoGenero?.Nombre ?? "(sin especificar)")
                .Select(g => (Etiqueta: g.Key, Conteo: g.Count()));

            // Normalizacion basica y temporal (seccion 13, pendiente de comite): trim +
            // minusculas para unir variantes triviales de escritura ("Migrante" / "migrante ").
            var autodescripcion = registrantesEnPeriodo
                .GroupBy(r => string.IsNullOrWhiteSpace(r.AutodescripcionCultural)
                    ? "(sin especificar)"
                    : r.AutodescripcionCultural.Trim().ToLowerInvariant())
                .Select(g => (Etiqueta: g.Key, Conteo: g.Count()));

            return new EstadisticasDemograficasViewModel
            {
                TotalRegistrantesParticipantes = registrantesEnPeriodo.Count,
                Genero = PrivacidadReporteHelper.AgruparConUmbralDePrivacidad(genero, umbral),
                AutodescripcionCultural = PrivacidadReporteHelper.AgruparConUmbralDePrivacidad(autodescripcion, umbral)
            };
        }

        // "RegistrantesAlumnosPorCarrera" necesita el mismo universo de registrantesEnPeriodo
        // que la demografia; se calcula aparte y se inyecta en el controlador para no duplicar
        // la construccion de ese universo dentro de ConstruirDistribucionAcademica.
        public static List<ConteoConPorcentaje> CalcularAlumnosPorCarrera(List<Registrante> registrantesEnPeriodo)
        {
            var alumnos = registrantesEnPeriodo.Where(r => r.Tipo == AltarWeb.Models.Registro.TipoRegistrante.Alumno).ToList();
            var total = alumnos.Count;

            return alumnos
                .GroupBy(r => r.CatalogoCarrera?.Nombre ?? "Sin especificar")
                .Select(g => new ConteoConPorcentaje
                {
                    Etiqueta = g.Key,
                    Conteo = g.Count(),
                    Porcentaje = total > 0 ? Math.Round(g.Count() * 100m / total, 1) : 0
                })
                .OrderByDescending(c => c.Conteo)
                .ToList();
        }

        // PRIV-02: generadoPor identifica al admin/juez cuya sesion disparo la descarga (no se loguea
        // en ningun otro lado; solo aparece impreso en el propio PDF como parte de la marca de
        // confidencialidad, igual que la fecha de generacion).
        public byte[] GenerarPdf(ReportePeriodoViewModel r, string generadoPor)
        {
            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(1.2f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));

                    // PRIV-02: marca de agua diagonal en cada pagina — el contenido agrega datos
                    // institucionales sensibles (demografia, promedios por juez) y no debe circular
                    // como si fuera un documento publico.
                    page.Foreground()
                        .AlignCenter()
                        .AlignMiddle()
                        .Rotate(-35)
                        .Text("CONFIDENCIAL")
                        .FontSize(72)
                        .Bold()
                        .FontColor(Colors.Grey.Lighten3);

                    page.Header().Column(col =>
                    {
                        col.Item().Text($"Reporte de Cierre de Periodo · {r.Periodo}").Bold().FontSize(18).FontColor("#00703c");
                        col.Item().Text($"Concurso de Altares de Muertos · FIM UABC · generado {r.GeneradoEn:dd/MM/yyyy HH:mm} por {generadoPor}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        col.Item().Text("CONFIDENCIAL — uso interno FIM/APFI. No distribuir fuera del comité organizador.")
                            .FontSize(8).Italic().Bold().FontColor(Colors.Red.Darken2);
                        col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().Component(new SeccionTitulo("1. Participación general"));
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            AddRow(t, "Total de equipos inscritos", r.ParticipacionGeneral.TotalEquipos.ToString());
                            AddRow(t, "Ficha de Registro completa", r.ParticipacionGeneral.EquiposFichaCompleta.ToString());
                            AddRow(t, "Ficha de Registro incompleta", r.ParticipacionGeneral.EquiposFichaIncompleta.ToString());
                            AddRow(t, "Evaluaciones Preliminar", r.ParticipacionGeneral.EvaluacionesPreliminar.ToString());
                            AddRow(t, "Evaluaciones Final", r.ParticipacionGeneral.EvaluacionesFinal.ToString());
                            AddRow(t, "Promedio de integrantes por equipo", r.ParticipacionGeneral.PromedioIntegrantesPorEquipo.ToString("0.0"));
                            foreach (var (tipo, conteo) in r.ParticipacionGeneral.RegistrantesPorTipo)
                            {
                                AddRow(t, $"Registrantes · {tipo}", conteo.ToString());
                            }
                        });

                        col.Item().Component(new SeccionTitulo("2. Distribución académica"));
                        col.Item().Text("Equipos por carrera").Bold().FontSize(10);
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); });
                            AddHeaderRow(t, "Carrera", "Equipos", "%");
                            foreach (var c in r.DistribucionAcademica.EquiposPorCarrera)
                            {
                                AddRow(t, c.Etiqueta, c.Conteo.ToString(), $"{c.Porcentaje}%");
                            }
                        });
                        col.Item().Text("Alumnos participantes por carrera").Bold().FontSize(10);
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); });
                            AddHeaderRow(t, "Carrera", "Alumnos", "%");
                            foreach (var c in r.DistribucionAcademica.RegistrantesAlumnosPorCarrera)
                            {
                                AddRow(t, c.Etiqueta, c.Conteo.ToString(), $"{c.Porcentaje}%");
                            }
                        });
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            foreach (var (etiqueta, conteo) in r.DistribucionAcademica.EquiposPorTipoAltar)
                            {
                                AddRow(t, $"Tipo de altar · {etiqueta}", conteo.ToString());
                            }
                            foreach (var (etiqueta, conteo) in r.DistribucionAcademica.EquiposPorNiveles)
                            {
                                AddRow(t, $"Niveles · {etiqueta}", conteo.ToString());
                            }
                            AddRow(t, "Equipos con Catrina", r.DistribucionAcademica.EquiposConCatrina.ToString());
                            AddRow(t, "Equipos sin Catrina", r.DistribucionAcademica.EquiposSinCatrina.ToString());
                        });

                        col.Item().Component(new SeccionTitulo("3. Resultados de evaluación (solo Final)"));
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            AddRow(t, "Nota Final · promedio", r.ResultadosEvaluacion.NotaFinalGeneral.Promedio?.ToString("0.00") ?? "—");
                            AddRow(t, "Nota Final · máximo", r.ResultadosEvaluacion.NotaFinalGeneral.Maximo?.ToString("0.00") ?? "—");
                            AddRow(t, "Nota Final · mínimo", r.ResultadosEvaluacion.NotaFinalGeneral.Minimo?.ToString("0.00") ?? "—");
                            AddRow(t, "Objetivo Cultural · promedio", r.ResultadosEvaluacion.PromedioObjetivoCultural?.ToString("0.00") ?? "—");
                            AddRow(t, "Esencia y Personalidad · promedio", r.ResultadosEvaluacion.PromedioEsenciaPersonalidad?.ToString("0.00") ?? "—");
                            AddRow(t, "Valoración General · promedio", r.ResultadosEvaluacion.PromedioValoracionGeneral?.ToString("0.00") ?? "—");
                            AddRow(t, "Distribución por Niveles · promedio", r.ResultadosEvaluacion.PromedioDistribucionNiveles?.ToString("0.00") ?? "—");
                            AddRow(t, "Narrador · promedio", r.ResultadosEvaluacion.PromedioNarrador?.ToString("0.00") ?? "—");
                            AddRow(t, "Nota de Catrina · promedio", r.ResultadosEvaluacion.PromedioNotaCatrina?.ToString("0.00") ?? "— (sin evaluaciones de Catrina)");
                        });

                        col.Item().Text("Nota Final por carrera").Bold().FontSize(10);
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                            AddHeaderRow(t, "Carrera", "Promedio", "Máx.", "Mín.");
                            foreach (var (carrera, stats) in r.ResultadosEvaluacion.NotaFinalPorCarrera)
                            {
                                AddRow(t, carrera, stats.Promedio?.ToString("0.00") ?? "—", stats.Maximo?.ToString("0.00") ?? "—", stats.Minimo?.ToString("0.00") ?? "—");
                            }
                        });

                        col.Item().Text("Podio del altar por carrera").Bold().FontSize(10);
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(2); c.RelativeColumn(); });
                            AddHeaderRow(t, "Carrera", "Lugar", "Equipo / Altar", "Nota");
                            foreach (var p in r.ResultadosEvaluacion.PodiumAltarPorCarrera)
                            {
                                AddRow(t, p.Carrera, $"{p.Lugar}°", $"{p.NombreEquipo} · {p.NombreAltar}", p.Nota?.ToString("0.00") ?? "—");
                            }
                        });

                        col.Item().Text("Podio de Catrina por carrera").Bold().FontSize(10);
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(2); c.RelativeColumn(); });
                            AddHeaderRow(t, "Carrera", "Lugar", "Equipo / Altar", "Nota");
                            foreach (var p in r.ResultadosEvaluacion.PodiumCatrinaPorCarrera)
                            {
                                AddRow(t, p.Carrera, $"{p.Lugar}°", $"{p.NombreEquipo} · {p.NombreAltar}", p.Nota?.ToString("0.00") ?? "—");
                            }
                        });

                        col.Item().Text("Elementos con más \"No presente\"").Bold().FontSize(10);
                        col.Item().Text(string.Join(", ", r.ResultadosEvaluacion.TopNoPresente.Select(e => $"{e.Elemento} ({e.Conteo})")));
                        col.Item().Text("Elementos con más \"Muy satisfactorio\"").Bold().FontSize(10);
                        col.Item().Text(string.Join(", ", r.ResultadosEvaluacion.TopMuySatisfactorio.Select(e => $"{e.Elemento} ({e.Conteo})")));

                        col.Item().Component(new SeccionTitulo("4. Participación de jueces y maestros"));
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            AddRow(t, "Jueces activos que evaluaron", r.ParticipacionJueces.TotalJuecesActivos.ToString());
                            AddRow(t, "Maestros encargados distintos", r.ParticipacionJueces.TotalMaestrosEncargadosDistintos.ToString());
                            foreach (var (juez, conteo) in r.ParticipacionJueces.EvaluacionesPorJuez)
                            {
                                AddRow(t, $"Evaluaciones · {juez}", conteo.ToString());
                            }
                        });

                        col.Item().Component(new SeccionTitulo("5. Estadísticas demográficas"));
                        col.Item().Text($"Umbral de agrupación de privacidad aplicado: N = {r.UmbralAgrupacionDemografica} (vision.md §12.2). Categorías con menos personas se agrupan como \"{PrivacidadReporteHelper.EtiquetaGrupoReducido}\".")
                            .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                        col.Item().Text("Género").Bold().FontSize(10);
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); });
                            foreach (var (etiqueta, conteo) in r.EstadisticasDemograficas.Genero)
                            {
                                AddRow(t, etiqueta, conteo.ToString());
                            }
                        });
                        col.Item().Text("Autodescripción cultural").Bold().FontSize(10);
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); });
                            foreach (var (etiqueta, conteo) in r.EstadisticasDemograficas.AutodescripcionCultural)
                            {
                                AddRow(t, etiqueta, conteo.ToString());
                            }
                        });
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().AlignCenter().Text("CONFIDENCIAL — uso interno FIM/APFI").FontSize(7).Italic().FontColor(Colors.Grey.Darken1);
                        col.Item().AlignCenter().Text(x =>
                        {
                            x.Span("Página ");
                            x.CurrentPageNumber();
                            x.Span(" de ");
                            x.TotalPages();
                        });
                    });
                });
            });

            return documento.GeneratePdf();
        }

        private static void AddHeaderRow(TableDescriptor t, params string[] valores)
        {
            foreach (var v in valores)
            {
                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(2).Text(v).Bold().FontSize(8);
            }
        }

        private static void AddRow(TableDescriptor t, params string[] valores)
        {
            for (int i = 0; i < valores.Length; i++)
            {
                var celda = t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(2).Text(valores[i]).FontSize(9);
                if (i == 0) celda.SemiBold();
            }
        }
    }

    // Componente reutilizable de titulo de seccion para el PDF (evita repetir el mismo
    // bloque de estilo 5 veces en GenerarPdf).
    internal class SeccionTitulo : IComponent
    {
        private readonly string _titulo;
        public SeccionTitulo(string titulo) { _titulo = titulo; }

        public void Compose(IContainer container)
        {
            container.PaddingTop(4).Column(col =>
            {
                col.Item().Text(_titulo).Bold().FontSize(13).FontColor("#00703c");
                col.Item().PaddingBottom(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            });
        }
    }
}
