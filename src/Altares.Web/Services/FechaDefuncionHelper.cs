using AltarWeb.Models.Registro;

namespace AltarWeb.Services
{
    // Regla de la fecha de defuncion del difunto del altar. La usan el portal del participante (CrearEquipo, Ficha)
    // y la edicion de equipos del admin, para que no vuelvan a divergir.
    //
    // A2 (pruebas adversariales 2026-10-21): sin registro de ConfiguracionPeriodo la regla "al menos 1 año de
    // fallecido" no se aplicaba, aunque la Ficha le anunciaba al participante que si (valor por defecto true). Y no
    // habia limite inferior ni prohibicion de fechas futuras con la regla apagada (se aceptaron 0001 y 2030).
    public static class FechaDefuncionHelper
    {
        public static readonly DateOnly FechaMinima = new(1900, 1, 1);

        // Sin registro de configuracion vale el valor por defecto de ConfiguracionPeriodo.
        public static bool ExigeMinimoUnAnio(ConfiguracionPeriodo? config) =>
            config?.ExigirMinimoUnAnioFallecimiento ?? new ConfiguracionPeriodo().ExigirMinimoUnAnioFallecimiento;

        // Devuelve el mensaje de error o null si la fecha es valida.
        public static string? Validar(DateOnly fecha, ConfiguracionPeriodo? config)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            if (fecha < FechaMinima) return "La fecha de defunción no es válida.";
            if (fecha > hoy) return "La fecha de defunción no puede ser futura.";
            if (ExigeMinimoUnAnio(config) && fecha > hoy.AddYears(-1)) return "La fecha de defunción debe ser de al menos 1 año atrás.";
            return null;
        }
    }
}
