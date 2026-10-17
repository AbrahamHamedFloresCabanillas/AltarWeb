namespace AltarWeb.Models.Registro
{
    // Ultimo consecutivo de CodigoPublico emitido por periodo. Solo lo escribe CodigoPublicoEquipo,
    // con un incremento atomico dentro de la transaccion que crea el equipo.
    public class ContadorCodigoPublico
    {
        public string Periodo { get; set; } = string.Empty;
        public int Ultimo { get; set; }
    }
}
