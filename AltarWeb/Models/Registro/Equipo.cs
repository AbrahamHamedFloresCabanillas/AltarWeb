using System.ComponentModel.DataAnnotations;

namespace AltarWeb.Models.Registro
{
    public class Equipo
    {
        public int Id { get; set; }

        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        public string NombreAltar { get; set; } = string.Empty;

        public int CarreraId { get; set; }
        public virtual CatalogoCarrera Carrera { get; set; } = null!;

        [Required]
        public string Periodo { get; set; } = string.Empty;

        public int? CreadoPorRegistranteId { get; set; }
        public virtual Registrante? CreadoPorRegistrante { get; set; }

        // MaestroEncargadoId es nulo mientras el maestro/administrativo capturado por el alumno
        // todavia no se registra en el sistema; mientras tanto se guarda su identificador aqui.
        // Cuando alguien se registre con ese identificador, RegistroController lo vincula
        // automaticamente y limpia MaestroEncargadoIdentificadorPendiente.
        public int? MaestroEncargadoId { get; set; }
        public virtual Registrante? MaestroEncargado { get; set; }

        public string? MaestroEncargadoIdentificadorPendiente { get; set; }

        public string? UbicacionAltar { get; set; }

        // Declarado por el equipo en la Ficha de Registro (no por el juez); determina si
        // la pestana de evaluacion de Catrina aparece en NuevaEvaluacion.
        public bool HaceCatrina { get; set; }

        public bool Activo { get; set; } = true;
        public DateTime CreadoEn { get; set; } = DateTime.Now;

        public virtual Difunto? Difunto { get; set; }
        public virtual Evaluacion? Evaluacion { get; set; }
        public virtual ICollection<RegistranteEquipo> Integrantes { get; set; } = new List<RegistranteEquipo>();
    }
}
