using System.Net;
using Catrinas.Tests.Infraestructura;

namespace Catrinas.Tests;

// Manuales de usuario en PDF (peticion del propietario, 2026-10-22): uno por rol (participante, jurado,
// administracion), en una pagina "Manuales" del menu lateral para el jurado y la administracion. El del participante
// tambien se enlaza desde su panel.
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class ManualesTests
{
    private readonly CatrinasFactory _f;
    public ManualesTests(CatrinasFactory f) => _f = f;

    private static async Task AssertPdfAsync(HttpResponseMessage respuesta, string nombreArchivo)
    {
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Contains(nombreArchivo, respuesta.Content.Headers.ContentDisposition?.ToString());
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 5_000, $"PDF demasiado pequeño ({bytes.Length} bytes)");
    }

    [Fact]
    public async Task Admin_VeElBotonAlFinalDeAdministracion_YDescargaLosTresManuales()
    {
        var admin = await Escenario.JuradoAsync(_f, admin: true);

        var inicio = await admin.GetStringAsync("/Evaluacion/Index");
        var configuracion = inicio.IndexOf("/Admin/Configuracion", StringComparison.Ordinal);
        var manuales = inicio.IndexOf("/Manual\"", StringComparison.Ordinal);
        Assert.True(configuracion >= 0 && manuales > configuracion, "El boton Manuales debe ir abajo de la seccion de administracion");

        var pagina = await admin.GetStringAsync("/Manual");
        Assert.Contains("/Manual/Participante", pagina);
        Assert.Contains("/Manual/Jurado", pagina);
        Assert.Contains("/Manual/Administracion", pagina);

        await AssertPdfAsync(await admin.GetAsync("/Manual/Participante"), "Manual-Participante-Concurso-de-Catrinas.pdf");
        await AssertPdfAsync(await admin.GetAsync("/Manual/Jurado"), "Manual-Jurado-Concurso-de-Catrinas.pdf");
        await AssertPdfAsync(await admin.GetAsync("/Manual/Administracion"), "Manual-Administracion-Concurso-de-Catrinas.pdf");
    }

    [Fact]
    public async Task Jurado_VeElBoton_PeroNoElManualDeAdministracion()
    {
        var jurado = await Escenario.JuradoAsync(_f);

        Assert.Contains("/Manual\"", await jurado.GetStringAsync("/Evaluacion/Index"));
        Assert.DoesNotContain("/Manual/Administracion", await jurado.GetStringAsync("/Manual"));
        await AssertPdfAsync(await jurado.GetAsync("/Manual/Jurado"), "Manual-Jurado");
        Assert.Equal(HttpStatusCode.Redirect, (await jurado.GetAsync("/Manual/Administracion")).StatusCode);
    }

    [Fact]
    public async Task Anonimo_SoloDescargaElManualDelParticipante()
    {
        var anonimo = _f.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await anonimo.GetAsync("/Manual")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonimo.GetAsync("/Manual/Jurado")).StatusCode);
        await AssertPdfAsync(await anonimo.GetAsync("/Manual/Participante"), "Manual-Participante");
    }

    [Fact]
    public async Task ElPanelDelParticipante_EnlazaSuManual()
    {
        var n = Escenario.Siguiente();
        var participante = _f.Cliente();
        Http.Redirige(await Http.SignupAsync(participante, "Participante Manual", $"10{n:D5}", $"manual.cat{n}@uabc.edu.mx"), "Panel");

        Assert.Contains("/Manual/Participante", await participante.GetStringAsync("/Registro/Panel"));
    }
}
