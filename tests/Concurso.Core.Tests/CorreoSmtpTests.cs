using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Concurso.Core.Correo;
using Microsoft.Extensions.Configuration;

namespace Concurso.Core.Tests;

// F4: errores de SMTP que las apps deben poder atrapar y mostrar como mensaje (no 500 ni peticion colgada).
public class CorreoSmtpTests
{
    private static ServicioCorreoSmtp Servicio(int puerto, int timeoutSegundos = 2) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "127.0.0.1",
            ["Smtp:Port"] = puerto.ToString(),
            ["Smtp:EnableSsl"] = "false",
            ["Smtp:User"] = "pruebas@ejemplo.test",
            ["Smtp:Password"] = "no-real",
            ["Smtp:TimeoutSegundos"] = timeoutSegundos.ToString()
        }).Build(), "Pruebas");

    private static Task Enviar(ServicioCorreoSmtp s) =>
        s.EnviarAsync("destino@ejemplo.test", "Asunto", "Cuerpo", new byte[] { 1, 2, 3 }, "a.pdf");

    [Fact]
    public async Task ServidorQueNoResponde_SeCortaPorTimeout()
    {
        // Acepta la conexion TCP y nunca manda el saludo SMTP.
        using var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        // La conexion aceptada se conserva hasta el final: si se descartaba, el recolector de basura podia
        // finalizar el socket y cerrar la conexion, y el cliente recibia SmtpException en vez del timeout
        // (la prueba fallaba de forma intermitente en las corridas completas).
        var aceptada = escucha.AcceptTcpClientAsync();

        var reloj = Stopwatch.StartNew();
        var error = await Assert.ThrowsAsync<TimeoutException>(() => Enviar(Servicio(puerto, timeoutSegundos: 2)));
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(15), $"Tardo {reloj.Elapsed.TotalSeconds:F1} s.");
        Assert.Contains("2 s", error.Message);
        (await aceptada).Dispose();
    }

    [Fact]
    public async Task ServidorInaccesible_LanzaSmtpException()
    {
        // Puerto que se abre y se cierra de inmediato: nadie escucha.
        var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        escucha.Stop();

        await Assert.ThrowsAsync<SmtpException>(() => Enviar(Servicio(puerto)));
    }

    [Fact]
    public async Task ConfiguracionIncompleta_LanzaInvalidOperation()
    {
        var servicio = new ServicioCorreoSmtp(new ConfigurationBuilder().Build(), "Pruebas");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Enviar(servicio));
    }
}
