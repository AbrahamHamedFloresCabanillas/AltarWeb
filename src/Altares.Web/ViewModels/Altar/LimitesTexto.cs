namespace AltarWeb.ViewModels.Altar
{
    // M2 (pruebas adversariales 2026-10-21): los datos del participante no tenian limite de longitud (se acepto un
    // nombre de 5,000 caracteres que genero una constancia de 9 paginas). Mismos topes que usa Catrinas. Solo
    // validan la entrada: las columnas no cambian, asi que no hay migracion.
    public static class LimitesTexto
    {
        public const int NombrePersona = 200;
        public const int Correo = 254;
        public const int Telefono = 20;
        public const int Autodescripcion = 500;
        public const int NombreEquipo = 150;
        public const int NombreAltar = 150;
        public const int NombreDifunto = 200;
        public const int Ubicacion = 200;

        public const string Mensaje = "Máximo {1} caracteres.";
    }
}
