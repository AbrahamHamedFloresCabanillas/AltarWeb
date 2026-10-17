using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http;

namespace Concurso.Core.Diagnostico
{
    // Datos para cerrar el gate R6 (ForwardedHeaders) en el hosting real y para vigilar memoria durante la
    // prueba de carga. Cada app lo expone SOLO a administradores. Nunca incluye cookies, cabeceras de
    // autorizacion, tokens ni datos personales: solo lo que se necesita para ver como llega la peticion
    // (IP, esquema, cabeceras X-Forwarded-*/X-Original-*) y el estado del proceso.
    public static class DiagnosticoServidor
    {
        private static readonly string[] CabecerasDeRed =
        {
            "X-Forwarded-For", "X-Forwarded-Proto", "X-Forwarded-Host",
            "X-Original-For", "X-Original-Proto", "X-Original-Host",
            "X-ARR-SSL", "X-ARR-LOG-ID", "CF-Connecting-IP", "X-Real-IP", "Forwarded"
        };

        public static IReadOnlyDictionary<string, object?> Construir(HttpContext http)
        {
            var proceso = Process.GetCurrentProcess();
            var gc = GC.GetGCMemoryInfo();
            var datos = new Dictionary<string, object?>
            {
                ["fechaUtc"] = DateTime.UtcNow.ToString("O"),
                ["red.remoteIpAddress"] = http.Connection.RemoteIpAddress?.ToString(),
                ["red.remotePort"] = http.Connection.RemotePort,
                ["red.localIpAddress"] = http.Connection.LocalIpAddress?.ToString(),
                ["red.localPort"] = http.Connection.LocalPort,
                ["red.scheme"] = http.Request.Scheme,
                ["red.isHttps"] = http.Request.IsHttps,
                ["red.host"] = http.Request.Host.Value,
                ["red.pathBase"] = http.Request.PathBase.Value,
                ["proceso.id"] = Environment.ProcessId,
                ["proceso.64bits"] = Environment.Is64BitProcess,
                ["proceso.procesadores"] = Environment.ProcessorCount,
                ["proceso.runtime"] = RuntimeInformation.FrameworkDescription,
                ["proceso.so"] = RuntimeInformation.OSDescription,
                ["proceso.inicioUtc"] = proceso.StartTime.ToUniversalTime().ToString("O"),
                ["memoria.workingSetMB"] = Math.Round(proceso.WorkingSet64 / 1048576.0, 1),
                ["memoria.privadaMB"] = Math.Round(proceso.PrivateMemorySize64 / 1048576.0, 1),
                ["memoria.picoWorkingSetMB"] = Math.Round(proceso.PeakWorkingSet64 / 1048576.0, 1),
                ["memoria.gcHeapMB"] = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1),
                ["memoria.gcLimiteDisponibleMB"] = Math.Round(gc.TotalAvailableMemoryBytes / 1048576.0, 1),
                ["memoria.gcServidor"] = System.Runtime.GCSettings.IsServerGC,
                ["gc.colecciones"] = $"{GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}",
                ["hilos"] = proceso.Threads.Count
            };

            foreach (var cabecera in CabecerasDeRed)
            {
                if (http.Request.Headers.TryGetValue(cabecera, out var valor)) datos["cabecera." + cabecera] = valor.ToString();
            }

            return datos;
        }
    }
}
