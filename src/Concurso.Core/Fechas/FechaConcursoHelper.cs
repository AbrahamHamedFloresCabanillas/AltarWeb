namespace Concurso.Core.Fechas
{
    // Fuente unica de "hoy" para todo lo que depende del calendario del concurso.
    //
    // Problema que resuelve: las fechas limite de ConfiguracionPeriodo son fechas de CALENDARIO
    // ("el 15 de octubre"), no instantes. Compararlas contra DateTime.Now usa la zona horaria del
    // servidor, y el hosting no esta en Mexicali (MonsterASP corre en Europa, Azure en UTC): con un
    // servidor en UTC+2 el dia cambia ~9 horas antes, asi que un equipo quedaria bloqueado la tarde
    // anterior a su fecha limite. Centralizando aqui, la regla es la misma en todos los endpoints y
    // no depende de donde se despliegue.
    //
    // Mexicali / Baja California usa horario del Pacifico y SI observa horario de verano (a
    // diferencia del resto de Mexico desde 2022), por eso se usa la zona completa y no un offset fijo.
    public static class FechaConcursoHelper
    {
        private static readonly TimeZoneInfo Zona = ResolverZona();

        public static DateTime AhoraLocal =>
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zona).DateTime;

        public static DateOnly HoyLocal => DateOnly.FromDateTime(AhoraLocal);

        // La fecha limite es INCLUSIVA: el dia configurado todavia permite la accion y el bloqueo
        // empieza al dia siguiente. Sin fecha configurada (null) no hay limite.
        public static bool LimitePasado(DateTime? limite) =>
            limite != null && HoyLocal > DateOnly.FromDateTime(limite.Value);

        public static string Formatear(DateTime fecha) =>
            fecha.ToString("d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-MX"));

        private static TimeZoneInfo ResolverZona()
        {
            // "America/Tijuana" es el id IANA (Linux y .NET con ICU); "Pacific Standard Time" es el
            // id de Windows. Se intentan ambos para que el mismo binario sirva en los dos hostings.
            foreach (var id in new[] { "America/Tijuana", "Pacific Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }

            return TimeZoneInfo.Local;
        }
    }
}
