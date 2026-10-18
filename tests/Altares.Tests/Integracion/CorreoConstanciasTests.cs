using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using AltarWeb.Models.Registro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Equipo = AltarWeb.Models.Registro.Equipo;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace Altares.Tests.Integracion;

// Hallazgo F4: en Altares "Enviar constancias por correo" solo atrapaba la configuracion SMTP incompleta;
// un error SMTP real (servidor caido, credenciales, tiempo agotado) terminaba en 500. Debe volver al
// detalle con un mensaje, sin registrar la direccion del destinatario.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class CorreoConstanciasTests
{
    private readonly AltarWebFactory _factory;
    public CorreoConstanciasTests(AltarWebFactory factory) => _factory = factory;

    [Fact]
    public async Task ErrorSmtp_NoEs500_YNoRegistraElCorreo()
    {
        const string correo = "destinatario.smtp.f4@uabc.edu.mx";
        int evaluacionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
            var registrante = new Registrante { Tipo = TipoRegistrante.Alumno, NombreCompleto = "Destinatario SMTP", Identificador = "9912345", CorreoInstitucional = correo };
            context.Registrantes.Add(registrante);
            var carreraId = await context.CatalogoCarreras.Select(c => c.Id).FirstAsync();
            var equipo = new Equipo { Nombre = "Equipo SMTP F4", NombreAltar = "Altar SMTP", CarreraId = carreraId, Periodo = "2033-1", CodigoPublico = "ALT-2033-1-S001", CreadoPorRegistrante = registrante };
            context.EquiposConcurso.Add(equipo);
            await context.SaveChangesAsync();
            var evaluacion = new Evaluacion { EquipoId = equipo.Id, Periodo = "2033-1", Niveles = 7, Estado = EstadoEvaluacion.Final, NotaFinal = 80, SnapshotNombreEquipo = equipo.Nombre };
            context.EvaluacionesConcurso.Add(evaluacion);
            await context.SaveChangesAsync();
            evaluacionId = evaluacion.Id;
        }

        await _factory.CrearJuezAsync("juez-smtp-f4", "Juez-Smtp-2026!");
        var cliente = _factory.CrearCliente();
        var html = await cliente.GetStringAsync("/Registro/Login");
        string Token(string h) => Regex.Match(h, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var login = await cliente.PostAsync("/Acceso/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["usuario"] = "juez-smtp-f4", ["password"] = "Juez-Smtp-2026!", ["__RequestVerificationToken"] = Token(html)
        }));
        Assert.Contains("Historial", login.Headers.Location!.OriginalString);

        var r = await cliente.PostAsync("/Constancia/EnviarTodas", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["evaluacionId"] = evaluacionId.ToString(), ["__RequestVerificationToken"] = Token(await cliente.GetStringAsync("/Registro/Login"))
        }));

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains($"Detalle/{evaluacionId}", r.Headers.Location!.OriginalString);
        Assert.Contains(_factory.Logs, l => l.Contains("Error al enviar constancias") && l.Contains("SmtpException"));
        Assert.DoesNotContain(_factory.Logs, l => l.Contains(correo, StringComparison.OrdinalIgnoreCase));
    }

    // La constancia del juez es por su participacion (vision.md §9), no por equipo: "Enviar constancias"
    // de cada evaluacion NO debe mandarla, o un juez con N equipos la recibiria N veces. Sigue descargable.
    // El SMTP de pruebas apunta a un puerto cerrado: cualquier intento de envio dejaria un error en el log.
    [Fact]
    public async Task EnviarTodas_NoEnviaLaConstanciaDelJuez_PeroSeDescarga()
    {
        await _factory.CrearJuezAsync("juez-varios-f4", "Juez-Varios-2026!", correo: "juez.varios.f4@uabc.edu.mx");
        var evaluaciones = new List<int>();
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
            var juezId = await context.Jueces.Where(j => j.Usuario == "juez-varios-f4").Select(j => j.Id).SingleAsync();
            var carreraId = await context.CatalogoCarreras.Select(c => c.Id).FirstAsync();
            for (var k = 1; k <= 2; k++)
            {
                // Sin creador, integrantes ni maestro: el unico destinatario posible seria el juez.
                var equipo = new Equipo { Nombre = $"Equipo juez F4 {k}", NombreAltar = "Altar", CarreraId = carreraId, Periodo = "2033-2", CodigoPublico = $"ALT-2033-2-J00{k}" };
                context.EquiposConcurso.Add(equipo);
                await context.SaveChangesAsync();
                var evaluacion = new Evaluacion { EquipoId = equipo.Id, JuezId = juezId, Periodo = "2033-2", Niveles = 7, Estado = EstadoEvaluacion.Final, NotaFinal = 70 + k, SnapshotNombreEquipo = equipo.Nombre };
                context.EvaluacionesConcurso.Add(evaluacion);
                await context.SaveChangesAsync();
                evaluaciones.Add(evaluacion.Id);
            }
        }

        var cliente = _factory.CrearCliente();
        string Token(string h) => Regex.Match(h, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var login = await cliente.PostAsync("/Acceso/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["usuario"] = "juez-varios-f4", ["password"] = "Juez-Varios-2026!", ["__RequestVerificationToken"] = Token(await cliente.GetStringAsync("/Registro/Login"))
        }));
        Assert.Contains("Historial", login.Headers.Location!.OriginalString);

        foreach (var id in evaluaciones)
        {
            var r = await cliente.PostAsync("/Constancia/EnviarTodas", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["evaluacionId"] = id.ToString(), ["__RequestVerificationToken"] = Token(await cliente.GetStringAsync("/Registro/Login"))
            }));
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.DoesNotContain(_factory.Logs, l => l.Contains($"Error al enviar constancias de la evaluación {id}"));

            var pdf = await cliente.GetAsync($"/Constancia/DescargarJuez?evaluacionId={id}");
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        }
    }
}
