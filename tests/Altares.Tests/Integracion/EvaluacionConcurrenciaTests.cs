using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Equipo = AltarWeb.Models.Registro.Equipo;

namespace Altares.Tests.Integracion;

// C2 (pruebas adversariales 2026-10-21): dos jueces abrian la misma evaluacion Preliminar y el segundo en
// guardar reemplazaba en silencio lo del primero (y la evaluacion quedaba a su nombre). Ahora el formulario
// lleva la version que se abrio y el servidor rechaza el guardado si la evaluacion cambio mientras tanto.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class EvaluacionConcurrenciaTests
{
    private const string MensajeConflicto = "Otro juez modificó esta evaluación mientras la tenías abierta";
    private static int _contador;

    private readonly AltarWebFactory _factory;
    public EvaluacionConcurrenciaTests(AltarWebFactory factory) => _factory = factory;

    // Pantalla abierta por un juez: token antiforgery y version de la evaluacion que vio.
    private sealed record Pantalla(string Token, string Version);

    private static async Task<Pantalla> AbrirAsync(HttpClient juez, int equipoId)
    {
        var html = await juez.GetStringAsync($"/AltarEvaluacion/NuevaEvaluacion?equipoId={equipoId}");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(token.Success, "Sin token antiforgery");
        var version = Regex.Match(html, "name=\"version\" value=\"([^\"]*)\"");
        return new Pantalla(token.Groups[1].Value, version.Success ? version.Groups[1].Value : string.Empty);
    }

    private static Task<HttpResponseMessage> GuardarAsync(HttpClient juez, int equipoId, Pantalla pantalla, decimal valoracion, bool conVersion = true)
    {
        var campos = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = pantalla.Token,
            ["equipoId"] = equipoId.ToString(), ["niveles"] = "7", ["tipoAltar"] = "Tradicional", ["semblanza"] = "Gustaba del mar",
            ["esenciaPersonalidad"] = "5", ["valoracionGeneral"] = valoracion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["distribucionNiveles"] = "5", ["narrador"] = "5", ["accion"] = "preliminar"
        };
        if (conVersion) campos["version"] = pantalla.Version;
        return juez.PostAsync("/AltarEvaluacion/GuardarEvaluacion", new FormUrlEncodedContent(campos));
    }

    private async Task<(HttpClient Cliente, int JuezId)> JuezAsync()
    {
        var usuario = $"juez-concurrencia-{Interlocked.Increment(ref _contador)}";
        await _factory.CrearJuezAsync(usuario, "Juez-Concurrencia-2026!");
        var juez = _factory.CrearCliente();
        var html = await juez.GetStringAsync("/Registro/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var login = await juez.PostAsync("/Acceso/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["usuario"] = usuario, ["password"] = "Juez-Concurrencia-2026!", ["__RequestVerificationToken"] = token
        }));
        Assert.Contains("Historial", login.Headers.Location!.OriginalString);
        using var scope = _factory.Services.CreateScope();
        var id = await scope.ServiceProvider.GetRequiredService<AltarDbContext>().Jueces.Where(j => j.Usuario == usuario).Select(j => j.Id).SingleAsync();
        return (juez, id);
    }

    private async Task<int> CrearEquipoAsync()
    {
        var n = Interlocked.Increment(ref _contador);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
        var equipo = new Equipo
        {
            Nombre = $"Equipo concurrencia {n}", NombreAltar = $"Altar concurrencia {n}",
            CarreraId = await context.CatalogoCarreras.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync(),
            Periodo = Concurso.Core.Fechas.PeriodoHelper.ObtenerPeriodoActual(), CodigoPublico = $"ALT-CONC-{n:D4}"
        };
        context.EquiposConcurso.Add(equipo);
        await context.SaveChangesAsync();
        return equipo.Id;
    }

    private (decimal Valoracion, int? JuezId) Guardada(int equipoId)
    {
        using var scope = _factory.Services.CreateScope();
        var e = scope.ServiceProvider.GetRequiredService<AltarDbContext>().EvaluacionesConcurso.AsNoTracking().Single(x => x.EquipoId == equipoId);
        return (e.ValoracionGeneral, e.JuezId);
    }

    private static async Task AssertConflictoAsync(HttpClient juez, HttpResponseMessage respuesta, int equipoId)
    {
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Contains($"NuevaEvaluacion?equipoId={equipoId}", respuesta.Headers.Location!.OriginalString);
        Assert.Contains(MensajeConflicto, WebUtility.HtmlDecode(await juez.GetStringAsync(respuesta.Headers.Location!.OriginalString)));
    }

    [Fact]
    public async Task SegundoJuezConPantallaVieja_EsRechazado_YSeConservaLoDelPrimero()
    {
        var (juezA, idA) = await JuezAsync();
        var (juezB, _) = await JuezAsync();
        var equipoId = await CrearEquipoAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await GuardarAsync(juezA, equipoId, await AbrirAsync(juezA, equipoId), 5)).StatusCode);

        // Los dos abren la Preliminar ya guardada; A guarda primero.
        var pantallaA = await AbrirAsync(juezA, equipoId);
        var pantallaB = await AbrirAsync(juezB, equipoId);
        Assert.Equal(HttpStatusCode.Redirect, (await GuardarAsync(juezA, equipoId, pantallaA, 6)).StatusCode);

        var respuestaB = await GuardarAsync(juezB, equipoId, pantallaB, 9);

        await AssertConflictoAsync(juezB, respuestaB, equipoId);
        Assert.Equal((6m, (int?)idA), Guardada(equipoId));
    }

    [Fact]
    public async Task EvaluacionCreadaPorOtroJuezMientrasSeCapturaba_NoSeSobrescribe()
    {
        var (juezA, idA) = await JuezAsync();
        var (juezB, _) = await JuezAsync();
        var equipoId = await CrearEquipoAsync();

        // B abre el equipo sin evaluacion; A la crea antes de que B guarde.
        var pantallaB = await AbrirAsync(juezB, equipoId);
        Assert.Equal(HttpStatusCode.Redirect, (await GuardarAsync(juezA, equipoId, await AbrirAsync(juezA, equipoId), 7)).StatusCode);

        var respuestaB = await GuardarAsync(juezB, equipoId, pantallaB, 2);

        await AssertConflictoAsync(juezB, respuestaB, equipoId);
        Assert.Equal((7m, (int?)idA), Guardada(equipoId));
    }

    [Fact]
    public async Task VersionAusente_EnEvaluacionExistente_EsRechazada()
    {
        var (juez, _) = await JuezAsync();
        var equipoId = await CrearEquipoAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await GuardarAsync(juez, equipoId, await AbrirAsync(juez, equipoId), 5)).StatusCode);

        var respuesta = await GuardarAsync(juez, equipoId, await AbrirAsync(juez, equipoId), 8, conVersion: false);

        await AssertConflictoAsync(juez, respuesta, equipoId);
        Assert.Equal(5m, Guardada(equipoId).Valoracion);
    }

    [Fact]
    public async Task MismoJuez_GuardandoVariasVecesConPantallaActual_Funciona()
    {
        var (juez, _) = await JuezAsync();
        var equipoId = await CrearEquipoAsync();
        foreach (var valor in new[] { 4m, 5m, 6m })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await GuardarAsync(juez, equipoId, await AbrirAsync(juez, equipoId), valor)).StatusCode);
        }
        Assert.Equal(6m, Guardada(equipoId).Valoracion);
    }
}
