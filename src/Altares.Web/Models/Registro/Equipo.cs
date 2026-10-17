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

        // LEGADO (F2, 2026-10-17): el Concurso de Catrinas se separo en Catrinas.Web. Altares ya no lee ni escribe
        // este campo; la columna se conserva fisicamente (datos historicos) hasta despues del concurso.
        public bool HaceCatrina { get; set; }

        public bool Activo { get; set; } = true;
        public DateTime CreadoEn { get; set; } = DateTime.Now;

        // Validacion administrativa de la Ficha de Registro: un administrador reviso el equipo y
        // acepta su ficha para efectos de SEGUIMIENTO, de modo que deje de listarse como pendiente.
        // Null = sin validar. No sustituye a EquipoValidacionHelper.EsFichaCompleta: cerrar una
        // evaluacion como Final sigue exigiendo los datos reales (snapshot y constancias los usan).
        // Se guarda tambien quien la valido, porque es una accion que relaja un aviso en el que
        // confian los jueces.
        public DateTime? FichaValidadaPorAdminEn { get; set; }
        public int? FichaValidadaPorJuezId { get; set; }

        public virtual Difunto? Difunto { get; set; }
        public virtual Evaluacion? Evaluacion { get; set; }
        public virtual ICollection<RegistranteEquipo> Integrantes { get; set; } = new List<RegistranteEquipo>();
    }
}
