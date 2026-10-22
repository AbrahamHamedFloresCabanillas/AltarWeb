using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Equipo = AltarWeb.Models.Registro.Equipo;

namespace Altares.Tests.Integracion;

// C1 (pruebas adversariales 2026-10-21): los sliders de la evaluacion van de 0 a 10, pero el servidor aceptaba
// cualquier numero. Con una peticion alterada un juez llevaba a un equipo al 1er lugar con nota 405.6, y un
// valor de 1000 desbordaba decimal(5,2) con un error 500. Ahora se rechaza como en Catrinas.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class CalificacionesRangoTests
{
    private const string MensajeRango = "Cada criterio debe calificarse entre 0 y 10.";
    private static int _contador;

    private readonly AltarWebFactory _factory;
    public CalificacionesRangoTests(AltarWebFactory factory) => _factory = factory;

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rutaFormulario, string accion, params (string, string)[] campos)
    {
        var html = await cliente.GetStringAsync(rutaFormulario);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(token.Success, $"Sin token antiforgery en {rutaFormulario}");
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = token.Groups[1].Value;
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }

    private async Task<HttpClient> JuezAsync()
    {
        var usuario = $"juez-rango-{Interlocked.Increment(ref _contador)}";
        await _factory.CrearJuezAsync(usuario, "Juez-Rango-2026!");
        var juez = _factory.CrearCliente();
        var login = await PostAsync(juez, "/Registro/Login", "/Acceso/Login", ("usuario", usuario), ("password", "Juez-Rango-2026!"));
        Assert.Contains("Historial", login.Headers.Location!.OriginalString);
        return juez;
    }

    private async Task<int> CrearEquipoAsync()
    {
        var n = Interlocked.Increment(ref _contador);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
        var equipo = new Equipo
        {
            Nombre = $"Equipo rango {n}", NombreAltar = $"Altar rango {n}",
            CarreraId = await context.CatalogoCarreras.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync(),
            Periodo = Concurso.Core.Fechas.PeriodoHelper.ObtenerPeriodoActual(), CodigoPublico = $"ALT-RANGO-{n:D4}"
        };
        context.EquiposConcurso.Add(equipo);
        await context.SaveChangesAsync();
        return equipo.Id;
    }

    private static Task<HttpResponseMessage> GuardarAsync(HttpClient juez, int equipoId, string campo, string valor)
    {
        var campos = new Dictionary<string, string>
        {
            ["equipoId"] = equipoId.ToString(), ["niveles"] = "7", ["tipoAltar"] = "Tradicional", ["semblanza"] = "Gustaba del mar",
            ["esenciaPersonalidad"] = "5", ["valoracionGeneral"] = "5", ["distribucionNiveles"] = "5", ["narrador"] = "5",
            ["accion"] = "preliminar"
        };
        campos[campo] = valor;
        return PostAsync(juez, $"/AltarEvaluacion/NuevaEvaluacion?equipoId={equipoId}", "/AltarEvaluacion/GuardarEvaluacion",
            campos.Select(c => (c.Key, c.Value)).ToArray());
    }

    private AltarWeb.Models.Registro.Evaluacion? Evaluacion(int equipoId)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AltarDbContext>().EvaluacionesConcurso.AsNoTracking()
            .FirstOrDefault(e => e.EquipoId == equipoId);
    }

    private static async Task AssertRechazadaAsync(HttpClient juez, HttpResponseMessage respuesta, int equipoId)
    {
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Contains($"NuevaEvaluacion?equipoId={equipoId}", respuesta.Headers.Location!.OriginalString);
        Assert.Contains(MensajeRango, WebUtility.HtmlDecode(await juez.GetStringAsync(respuesta.Headers.Location!.OriginalString)));
    }

    [Theory]
    [InlineData("valoracionGeneral", "999")]
    [InlineData("distribucionNiveles", "1000")]
    [InlineData("narrador", "-1")]
    [InlineData("esenciaPersonalidad", "10.01")]
    [InlineData("valoracionGeneral", "abc")]
    public async Task EvaluacionNueva_ConCriterioFueraDeRango_NoSeGuarda(string campo, string valor)
    {
        var juez = await JuezAsync();
        var equipoId = await CrearEquipoAsync();

        var respuesta = await GuardarAsync(juez, equipoId, campo, valor);

        await AssertRechazadaAsync(juez, respuesta, equipoId);
        Assert.Null(Evaluacion(equipoId));
    }

    [Fact]
    public async Task PreliminarExistente_ConMil_NoDa500NiCambiaLosValores()
    {
        var juez = await JuezAsync();
        var equipoId = await CrearEquipoAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await GuardarAsync(juez, equipoId, "valoracionGeneral", "6")).StatusCode);
        var antes = Evaluacion(equipoId)!;

        var respuesta = await GuardarAsync(juez, equipoId, "valoracionGeneral", "1000");

        await AssertRechazadaAsync(juez, respuesta, equipoId);
        var despues = Evaluacion(equipoId)!;
        Assert.Equal(6m, despues.ValoracionGeneral);
        Assert.Equal(antes.NotaFinal, despues.NotaFinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10")]
    public async Task LimitesExactos_SeAceptan(string valor)
    {
        var juez = await JuezAsync();
        var equipoId = await CrearEquipoAsync();

        var respuesta = await GuardarAsync(juez, equipoId, "valoracionGeneral", valor);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(decimal.Parse(valor), Evaluacion(equipoId)!.ValoracionGeneral);
    }
}
