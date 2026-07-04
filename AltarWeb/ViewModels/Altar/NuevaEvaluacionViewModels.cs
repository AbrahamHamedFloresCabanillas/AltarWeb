using AltarWeb.Models.Registro;

namespace AltarWeb.ViewModels.Altar
{
    public class EquipoBusquedaItemViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Carrera { get; set; } = string.Empty;
        public int IntegrantesCount { get; set; }
        public bool YaEvaluado { get; set; }
    }

    public class ElementoChecklistItemViewModel
    {
        public int ElementoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public CategoriaElemento Categoria { get; set; }

        // Nivel sugerido por el catalogo (solo referencia, mini-manual). El nivel real donde
        // el juez coloco el elemento en ESTA evaluacion es NivelAsignado, elegido a mano.
        public int? NivelSugerido { get; set; }
        public int? NivelAsignado { get; set; }

        public string Significado { get; set; } = string.Empty;
        public string Colocacion { get; set; } = string.Empty;
        public Satisfaccion Satisfaccion { get; set; } = Satisfaccion.NoPresente;
        public bool Tematizado { get; set; }
    }

    public class NuevaEvaluacionViewModel
    {
        public string Busqueda { get; set; } = string.Empty;
        public List<EquipoBusquedaItemViewModel> Resultados { get; set; } = new();

        public int? EquipoId { get; set; }
        public int? EvaluacionId { get; set; }
        public bool SoloLectura { get; set; }
        public bool EsPreliminarExistente { get; set; }

        // Equipo (solo lectura)
        public string NombreEquipo { get; set; } = string.Empty;
        public string NombreAltar { get; set; } = string.Empty;
        public string Carrera { get; set; } = string.Empty;
        public string UbicacionAltar { get; set; } = string.Empty;
        public string ResponsableNombre { get; set; } = string.Empty;
        public string ResponsableTelefono { get; set; } = string.Empty;
        public string ResponsableCorreo { get; set; } = string.Empty;
        public string MaestroEncargado { get; set; } = string.Empty;
        public List<RegistranteEquipo> Integrantes { get; set; } = new();

        // Evaluacion del altar
        public string DifuntoNombre { get; set; } = string.Empty;
        public DateOnly DifuntoFechaDefuncion { get; set; }
        public TipoAltar TipoAltar { get; set; }
        public string SemblanzaHobbiesTematica { get; set; } = string.Empty;
        public int Niveles { get; set; } = 7;

        public List<ElementoChecklistItemViewModel> Elementos { get; set; } = new();

        public decimal PuntajeElementos { get; set; }
        public decimal ObjetivoCultural { get; set; }
        public decimal EsenciaPersonalidad { get; set; }
        public decimal ValoracionGeneral { get; set; }
        public decimal DistribucionNiveles { get; set; }
        public decimal Narrador { get; set; }
        public decimal NotaFinalPreview { get; set; }

        // Catrina — declarado por el equipo en su Ficha de Registro, de solo lectura aqui.
        public bool HaceCatrina { get; set; }
        public decimal SombreroTocado { get; set; }
        public decimal Guantes { get; set; }
        public decimal Vestimenta { get; set; }
        public decimal Zapatos { get; set; }
        public decimal Collar { get; set; }
        public decimal Maquillaje { get; set; }

        // Pesos/escala vigentes (para el calculo en vivo en JS)
        public decimal PesoObjetivoCultural { get; set; }
        public decimal PesoEsenciaPersonalidad { get; set; }
        public decimal PesoValoracionGeneral { get; set; }
        public decimal PesoDistribucionNiveles { get; set; }
        public decimal PesoNarrador { get; set; }
        public decimal PesoElementoRitual { get; set; }
        public decimal PesoElementoDecorativo { get; set; }
        public decimal ValorSatisfaccionNoPresente { get; set; }
        public decimal ValorSatisfaccionPoco { get; set; }
        public decimal ValorSatisfaccionSatisfactorio { get; set; }
        public decimal ValorSatisfaccionMuySatisfactorio { get; set; }
        public decimal BonusPorElementoTematizado { get; set; }
    }
}
