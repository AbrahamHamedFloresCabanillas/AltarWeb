using System.IO.Compression;
using System.Net;
using Catrinas.Tests.Infraestructura;
using Catrinas.Web.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Tests;

[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class EvaluacionTests
{
    private readonly CatrinasFactory _f;
    public EvaluacionTests(CatrinasFactory f) => _f = f;

    // 16, 17, 18, 21. Preliminar, edicion del preliminar y Final con confirmacion; nota ponderada
    // 10·25% + 8·20% + 6·10% + 4·20% + 2·25% = 6.00 con los pesos por defecto; snapshot completo.
    [Fact]
    public async Task Preliminar_Edicion_YFinal_ConNotaYSnapshot()
    {
        var e = await Escenario.CrearAsync(_f, carreraId: 4, conMaestro: true);
        var jurado = await Escenario.JuradoAsync(_f);

        Http.Redirige(await Http.GuardarEvaluacionAsync(jurado, e.InscripcionId, "preliminar", 5, 5, 5, 5, 5), "Evaluar");
        var preliminar = Escenario.Evaluacion(_f, e.InscripcionId)!;
        Assert.Equal(EstadoEvaluacion.Preliminar, preliminar.Estado);
        Assert.Equal(5.00m, preliminar.NotaCatrina);

        Http.Redirige(await Http.GuardarEvaluacionAsync(jurado, e.InscripcionId, "preliminar", 10, 8, 6, 4, 2), "Evaluar");
        Assert.Equal(6.00m, Escenario.Evaluacion(_f, e.InscripcionId)!.NotaCatrina);

        Http.Redirige(await Http.GuardarEvaluacionAsync(jurado, e.InscripcionId, "final", 10, 8, 6, 4, 2), "Detalle");
        var final = Escenario.Evaluacion(_f, e.InscripcionId)!;
        Assert.Equal(EstadoEvaluacion.Final, final.Estado);
        Assert.NotNull(final.FinalizadaEn);
        Assert.Equal(6.00m, final.NotaCatrina);
        Assert.Equal(new[] { 0.25m, 0.20m, 0.10m, 0.20m, 0.25m },
            new[] { final.PesoVestuario, final.PesoMaquillaje, final.PesoAccesorios, final.PesoOriginalidadCreatividad, final.PesoApegoTradiciones });
        Assert.Equal(e.Nombre, final.SnapshotNombreInscripcion);
        Assert.Equal("Ing. Civil", final.SnapshotCarrera);
        Assert.NotNull(final.SnapshotMaestroEncargado);
        Assert.Equal(2, final.Integrantes.Count);
        Assert.Single(final.Integrantes, i => i.Rol == RolInscripcion.Personificador);

        // Ya cerrada: no se modifica ni se reabre.
        await Http.GuardarEvaluacionAsync(jurado, e.InscripcionId, "preliminar", 1, 1, 1, 1, 1);
        Assert.Equal(6.00m, Escenario.Evaluacion(_f, e.InscripcionId)!.NotaCatrina);
        Assert.Equal(EstadoEvaluacion.Final, Escenario.Evaluacion(_f, e.InscripcionId)!.Estado);

        // Con evaluacion la inscripcion queda cerrada para el responsable.
        await Http.PostDesdeAsync(e.Responsable, "/Inscripcion/Editar", "/Inscripcion/Editar", ("Nombre", "Cambio tardio"), ("CarreraId", "4"));
        Assert.Equal(e.Nombre, _f.ConContexto(c => c.Inscripciones.Single(i => i.Id == e.InscripcionId).Nombre));
    }

    // 19. Requisitos del Final: Personificador designado y confirmacion explicita.
    [Fact]
    public async Task Final_ExigePersonificadorYConfirmacion()
    {
        var sinPersonificador = await Escenario.CrearAsync(_f, conPersonificador: false);
        var jurado = await Escenario.JuradoAsync(_f);

        Http.Redirige(await Http.GuardarEvaluacionAsync(jurado, sinPersonificador.InscripcionId, "final", 9, 9, 9, 9, 9), "Evaluar");
        Assert.Null(Escenario.Evaluacion(_f, sinPersonificador.InscripcionId));

        var lista = await Escenario.CrearAsync(_f);
        Http.Redirige(await Http.GuardarEvaluacionAsync(jurado, lista.InscripcionId, "final", 9, 9, 9, 9, 9, confirmar: false), "Evaluar");
        Assert.Null(Escenario.Evaluacion(_f, lista.InscripcionId));
    }

    // 21. Criterios fuera de 0-10 se rechazan (servidor y CHECK de la BD).
    [Fact]
    public async Task CriteriosFueraDeRango_SeRechazan()
    {
        var e = await Escenario.CrearAsync(_f);
        var jurado = await Escenario.JuradoAsync(_f);
        await Http.GuardarEvaluacionAsync(jurado, e.InscripcionId, "preliminar", 11, 5, 5, 5, 5);
        await Http.GuardarEvaluacionAsync(jurado, e.InscripcionId, "preliminar", 5, -1, 5, 5, 5);
        Assert.Null(Escenario.Evaluacion(_f, e.InscripcionId));

        Http.Redirige(await Http.GuardarEvaluacionAsync(jurado, e.InscripcionId, "preliminar", 5, 5, 5, 5, 5), "Evaluar");
        await Assert.ThrowsAsync<DbUpdateException>(() => _f.ConContextoAsync(async c =>
        {
            (await c.Evaluaciones.SingleAsync(x => x.InscripcionId == e.InscripcionId)).Vestuario = 12;
            await c.SaveChangesAsync();
        }));
    }

    // 20. Concurrencia: una evaluacion abierta y modificada por otro juez no se pisa (rowversion), y la
    //     segunda creacion simultanea la impide el indice unico. Nunca HTTP 500.
    [Fact]
    public async Task Concurrencia_EntreJueces()
    {
        var e = await Escenario.CrearAsync(_f);
        var juezA = await Escenario.JuradoAsync(_f);
        var juezB = await Escenario.JuradoAsync(_f);

        // Creacion simultanea.
        var creaciones = await Task.WhenAll(
            Http.GuardarEvaluacionAsync(juezA, e.InscripcionId, "preliminar", 5, 5, 5, 5, 5),
            Http.GuardarEvaluacionAsync(juezB, e.InscripcionId, "preliminar", 6, 6, 6, 6, 6));
        Assert.All(creaciones, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
        Assert.Equal(1, _f.ConContexto(c => c.Evaluaciones.Count(x => x.InscripcionId == e.InscripcionId)));

        // A abre la pantalla (rowversion vieja); B guarda; A intenta cerrar Final con datos viejos.
        var html = await juezA.GetStringAsync($"/Evaluacion/Evaluar?inscripcionId={e.InscripcionId}");
        var versionVieja = System.Text.RegularExpressions.Regex.Match(html, "name=\"rowVersion\" value=\"([^\"]*)\"").Groups[1].Value;
        Http.Redirige(await Http.GuardarEvaluacionAsync(juezB, e.InscripcionId, "preliminar", 7, 7, 7, 7, 7), "Evaluar");
        Http.Redirige(await Http.GuardarEvaluacionAsync(juezA, e.InscripcionId, "final", 9, 9, 9, 9, 9, rowVersion: versionVieja), "Evaluar");

        var actual = Escenario.Evaluacion(_f, e.InscripcionId)!;
        Assert.Equal(EstadoEvaluacion.Preliminar, actual.Estado);
        Assert.Equal(7.00m, actual.NotaCatrina);
    }

    // Hallazgo de la prueba de carga (F4): varios jueces cerrando Final a la vez en la MISMA carrera
    // chocaban al recalcular lugares (conflicto de rowversion -> 500) o podian dejar lugares de una foto
    // vieja. Ahora el recalculo se serializa y al final los lugares coinciden con las notas.
    [Fact]
    public async Task CierresFinalSimultaneos_MismaCarrera_SinErroresYLugaresCorrectos()
    {
        const int carrera = 9;
        var escenarios = new List<Escenario>();
        for (var k = 0; k < 8; k++) escenarios.Add(await Escenario.CrearAsync(_f, carreraId: carrera));
        var jueces = new List<HttpClient>();
        for (var k = 0; k < 8; k++) jueces.Add(await Escenario.JuradoAsync(_f));

        // Notas distintas para que el orden esperado sea inequivoco: 3, 4, ..., 10.
        var respuestas = await Task.WhenAll(escenarios.Select((e, k) =>
            Http.GuardarEvaluacionAsync(jueces[k], e.InscripcionId, "final", 3 + k, 3 + k, 3 + k, 3 + k, 3 + k)));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
        var finales = escenarios.Select(e => Escenario.Evaluacion(_f, e.InscripcionId)!).ToList();
        Assert.All(finales, f => Assert.Equal(EstadoEvaluacion.Final, f.Estado));
        Assert.Equal(new int?[] { null, null, null, null, null, 3, 2, 1 }, finales.Select(f => f.Lugar).ToArray());
    }

    // Hallazgo de la prueba de carga (F4): un rowVersion alterado o mal formado devolvia 500
    // (FormatException en Convert.FromBase64String). Ahora se trata como conflicto y no guarda nada.
    [Theory]
    [InlineData("no-es-base64!")]
    [InlineData("&#x2B;AAAAAAAAB9E=")]
    [InlineData("AAAA")]
    public async Task RowVersionMalFormado_NoEs500NiGuarda(string rowVersion)
    {
        var e = await Escenario.CrearAsync(_f);
        var juez = await Escenario.JuradoAsync(_f);
        Http.Redirige(await Http.GuardarEvaluacionAsync(juez, e.InscripcionId, "preliminar", 5, 5, 5, 5, 5), "Evaluar");

        var r = await Http.GuardarEvaluacionAsync(juez, e.InscripcionId, "final", 9, 9, 9, 9, 9, rowVersion: rowVersion);

        Http.Redirige(r, "Evaluar");
        var actual = Escenario.Evaluacion(_f, e.InscripcionId)!;
        Assert.Equal(EstadoEvaluacion.Preliminar, actual.Estado);
        Assert.Equal(5.00m, actual.NotaCatrina);
    }

    // 22. Pesos invalidos: la configuracion rechaza sumas distintas de 100 y la BD tambien.
    //     Cambiar pesos despues no recalcula evaluaciones ya cerradas.
    [Fact]
    public async Task Pesos_Validacion_YSinRecalculoHistorico()
    {
        var e = await Escenario.CrearAsync(_f);
        var admin = await Escenario.JuradoAsync(_f, admin: true);
        Http.Redirige(await Http.GuardarEvaluacionAsync(admin, e.InscripcionId, "final", 10, 0, 0, 0, 0), "Detalle");
        Assert.Equal(2.50m, Escenario.Evaluacion(_f, e.InscripcionId)!.NotaCatrina);

        var periodo = Concurso.Core.Fechas.PeriodoHelper.ObtenerPeriodoActual();
        (string, string)[] Pesos(string v, string m, string a, string o, string t) => new[]
        {
            ("PesoVestuarioPct", v), ("PesoMaquillajePct", m), ("PesoAccesoriosPct", a),
            ("PesoOriginalidadCreatividadPct", o), ("PesoApegoTradicionesPct", t), ("UmbralAgrupacionDemografica", "5")
        };

        var invalido = await Http.PostDesdeAsync(admin, "/Admin/Configuracion", "/Admin/Configuracion", Pesos("50", "50", "10", "0", "0"));
        Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
        Assert.Contains("deben sumar 100%", await invalido.Content.ReadAsStringAsync());
        var umbral = await Http.PostDesdeAsync(admin, "/Admin/Configuracion", "/Admin/Configuracion",
            Pesos("20", "20", "20", "20", "20").Select(p => p.Item1 == "UmbralAgrupacionDemografica" ? (p.Item1, "0") : p).ToArray());
        Assert.Equal(HttpStatusCode.OK, umbral.StatusCode);

        try
        {
            Http.Redirige(await Http.PostDesdeAsync(admin, "/Admin/Configuracion", "/Admin/Configuracion", Pesos("20", "20", "20", "20", "20")), "Configuracion");
            Assert.Equal(0.20m, _f.ConContexto(c => c.Configuraciones.Single(x => x.Periodo == periodo).PesoVestuario));

            // La evaluacion cerrada conserva su nota y sus pesos.
            var cerrada = Escenario.Evaluacion(_f, e.InscripcionId)!;
            Assert.Equal(2.50m, cerrada.NotaCatrina);
            Assert.Equal(0.25m, cerrada.PesoVestuario);

            await Assert.ThrowsAsync<DbUpdateException>(() => _f.ConContextoAsync(async c =>
            {
                (await c.Configuraciones.SingleAsync(x => x.Periodo == periodo)).PesoVestuario = 0.9m;
                await c.SaveChangesAsync();
            }));
        }
        finally
        {
            await Http.PostDesdeAsync(admin, "/Admin/Configuracion", "/Admin/Configuracion", Pesos("25", "20", "10", "20", "25"));
        }
    }

    // 23. Ranking por carrera: notas iguales comparten lugar (sin desempate inventado) y otra carrera
    //     no se mezcla.
    [Fact]
    public async Task Ranking_PorCarrera_ConEmpates()
    {
        var carrera = 11; // Mecatronica
        var a = await Escenario.CrearAsync(_f, carreraId: carrera);
        var b = await Escenario.CrearAsync(_f, carreraId: carrera);
        var c = await Escenario.CrearAsync(_f, carreraId: carrera);
        var d = await Escenario.CrearAsync(_f, carreraId: carrera);
        var otra = await Escenario.CrearAsync(_f, carreraId: 12);
        var jurado = await Escenario.JuradoAsync(_f);

        await Http.GuardarEvaluacionAsync(jurado, a.InscripcionId, "final", 9, 9, 9, 9, 9);
        await Http.GuardarEvaluacionAsync(jurado, b.InscripcionId, "final", 9, 9, 9, 9, 9);
        await Http.GuardarEvaluacionAsync(jurado, c.InscripcionId, "final", 8, 8, 8, 8, 8);
        await Http.GuardarEvaluacionAsync(jurado, d.InscripcionId, "final", 7, 7, 7, 7, 7);
        await Http.GuardarEvaluacionAsync(jurado, otra.InscripcionId, "final", 1, 1, 1, 1, 1);

        Assert.Equal(1, Escenario.Evaluacion(_f, a.InscripcionId)!.Lugar);
        Assert.Equal(1, Escenario.Evaluacion(_f, b.InscripcionId)!.Lugar);
        Assert.Equal(3, Escenario.Evaluacion(_f, c.InscripcionId)!.Lugar);
        Assert.Null(Escenario.Evaluacion(_f, d.InscripcionId)!.Lugar);
        Assert.Equal(1, Escenario.Evaluacion(_f, otra.InscripcionId)!.Lugar);

        var ranking = await jurado.GetStringAsync("/Evaluacion/Ranking");
        Assert.Contains("Ing. en Mecatrónica", System.Net.WebUtility.HtmlDecode(ranking));
    }
}

[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class ConstanciaTests
{
    private readonly CatrinasFactory _f;
    public ConstanciaTests(CatrinasFactory f) => _f = f;

    private async Task<byte[]> BytesAsync(HttpClient c, string ruta)
    {
        var r = await c.GetAsync(ruta);
        if (r.StatusCode != HttpStatusCode.OK)
        {
            var errores = string.Join(Environment.NewLine, _f.Logs.Where(l => l.Contains("Exception") || l.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(5));
            Assert.Fail($"{ruta} -> {(int)r.StatusCode}{Environment.NewLine}{errores}");
        }
        return await r.Content.ReadAsByteArrayAsync();
    }

    private static void EsPdf(byte[] b) => Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(b, 0, 4));

    // 24, 25, 26. Generacion real de cada tipo y descarga propia.
    [Fact]
    public async Task CadaTipoDeConstancia_SeGeneraDeVerdad()
    {
        var e = await Escenario.CrearAsync(_f, conMaestro: true);
        var jurado = await Escenario.JuradoAsync(_f);
        Http.Redirige(await Http.GuardarEvaluacionAsync(jurado, e.InscripcionId, "final", 9, 9, 9, 9, 9), "Detalle");
        var id = Escenario.Evaluacion(_f, e.InscripcionId)!.Id;

        EsPdf(await BytesAsync(jurado, $"/Constancia/Grupal?evaluacionId={id}"));
        EsPdf(await BytesAsync(jurado, $"/Constancia/Maestro?evaluacionId={id}"));
        EsPdf(await BytesAsync(jurado, $"/Constancia/Jurado?evaluacionId={id}"));

        using var zip = new ZipArchive(new MemoryStream(await BytesAsync(jurado, $"/Constancia/Individuales?evaluacionId={id}")));
        Assert.Equal(2, zip.Entries.Count);
        foreach (var entrada in zip.Entries)
        {
            using var flujo = new MemoryStream();
            entrada.Open().CopyTo(flujo);
            EsPdf(flujo.ToArray());
        }

        EsPdf(await BytesAsync(e.Integrante, "/Constancia/Mia"));
        EsPdf(await BytesAsync(e.Responsable, "/Constancia/Mia"));
    }

    // 27. Permisos: sin Final no hay constancias; un participante no descarga las del jurado; quien no
    //     participa no tiene constancia propia.
    [Fact]
    public async Task Permisos()
    {
        var e = await Escenario.CrearAsync(_f);
        var jurado = await Escenario.JuradoAsync(_f);
        Http.Redirige(await Http.GuardarEvaluacionAsync(jurado, e.InscripcionId, "preliminar", 9, 9, 9, 9, 9), "Evaluar");
        var id = Escenario.Evaluacion(_f, e.InscripcionId)!.Id;

        Http.Redirige(await jurado.GetAsync($"/Constancia/Grupal?evaluacionId={id}"), "Detalle");
        Http.Redirige(await e.Integrante.GetAsync("/Constancia/Mia"), "Panel");
        Http.Redirige(await e.Integrante.GetAsync($"/Constancia/Grupal?evaluacionId={id}"), "tab=jurado");

        var ajeno = _f.Cliente();
        var n = Escenario.Siguiente();
        await Http.SignupAsync(ajeno, "Sin Inscripcion", $"12{n:D5}", $"ajeno{n}@uabc.edu.mx");
        Http.Redirige(await ajeno.GetAsync("/Constancia/Mia"), "Panel");
        Http.Redirige(await _f.Cliente().GetAsync("/Constancia/Mia"), "/");
    }
}
