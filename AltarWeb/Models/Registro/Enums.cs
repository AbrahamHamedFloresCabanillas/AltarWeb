namespace AltarWeb.Models.Registro
{
    public enum TipoRegistrante
    {
        Alumno,
        Maestro,
        Administrativo
    }

    public enum RolEquipo
    {
        Integrante,
        Narrador
    }

    public enum TipoAltar
    {
        Tradicional,
        Ninos
    }

    public enum EstadoEvaluacion
    {
        Preliminar,
        Final
    }

    public enum CategoriaElemento
    {
        RitualObligatorio,
        Decorativo
    }

    // Escala de satisfaccion por elemento (vision.md 7.2). Tres niveles desde 2026-09:
    // "No lo tiene" / "Regular" / "Bueno". Se persiste como texto (ElementoEvaluadoConfiguration),
    // asi que renombrar un miembro exige migracion de datos.
    public enum Satisfaccion
    {
        NoLoTiene,
        Regular,
        Bueno
    }
}
