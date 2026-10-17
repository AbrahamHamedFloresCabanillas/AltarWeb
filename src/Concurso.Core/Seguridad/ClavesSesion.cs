namespace Concurso.Core.Seguridad
{
    // Claves de HttpContext.Session que usan los controles transversales de Core. Las apps guardan
    // aqui la identidad del backoffice (JuezId/JuezRol); el resto de claves de sesion son propias de
    // cada aplicacion.
    public static class ClavesSesion
    {
        public const string JuezId = "JuezId";
    }
}
