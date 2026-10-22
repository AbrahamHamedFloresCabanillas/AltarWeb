using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using AltarWeb.Models.Registro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Equipo = AltarWeb.Models.Registro.Equipo;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace Altares.Tests.Integracion;

// Flujo completo de Altares tras retirar Catrina (F2), por HTTP contra la app real:
// registro -> equipo (con CodigoPublico) -> ficha -> integrantes -> Narrador -> evaluacion preliminar
// -> final -> ranking -> constancias -> reporte de cierre. Verifica tambien que ninguna pantalla ni POST
// activo permite evaluar o configurar Catrina, aunque el equipo tenga el flag legado HaceCatrina=1.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class FlujoAltaresTests
{
    private readonly AltarWebFactory _factory;

    public FlujoAltaresTests(AltarWebFactory factory) => _factory = factory;

    private static async Task<string> TokenAsync(HttpClient cliente, string ruta)
    {
        var html = await cliente.GetStringAsync(ruta);
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(m.Success, $"Sin token antiforgery en {ruta}");
        return m.Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rutaFormulario, string accion, params (string, string)[] campos)
    {
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = await TokenAsync(cliente, rutaFormulario);
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }

    private static Task<HttpResponseMessage> SignupAsync(HttpClient cliente, string nombre, string identificador, string correo) =>
        PostAsync(cliente, "/Registro/Signup", "/Registro/Signup",
            ("NombreCompleto", nombre), ("Identificador", identificador), ("CorreoInstitucional", correo),
            ("Password", "Participante-2026!"), ("ConfirmarPassword", "Participante-2026!"));

    private static void AssertRedirect(HttpResponseMessage r, string contiene)
    {
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains(contiene, r.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task FlujoCompletoDeAltares_FuncionaSinCatrina()
    {
        var sufijo = Random.Shared.Next(100, 999).ToString();
        int carreraId;
        using (var scope = _factory.Services.CreateScope())
        {
            carreraId = await scope.ServiceProvider.GetRequiredService<AltarDbContext>().CatalogoCarreras
                .Where(c => c.Activo).OrderBy(c => c.Id).Select(c => c.Id).FirstAsync();
        }

        // 1. Registro: maestro (5 digitos), organizador e integrante (7 digitos).
        var maestro = _factory.CrearCliente();
        var organizador = _factory.CrearCliente();
        var integrante = _factory.CrearCliente();
        AssertRedirect(await SignupAsync(maestro, "Maestra Flujo", $"18{sufijo}", $"maestra.flujo{sufijo}@uabc.edu.mx"), "Dashboard");
        AssertRedirect(await SignupAsync(organizador, "Organizador Flujo", $"1800{sufijo}", $"org.flujo{sufijo}@uabc.edu.mx"), "Dashboard");
        AssertRedirect(await SignupAsync(integrante, "Integrante Flujo", $"1801{sufijo}", $"int.flujo{sufijo}@uabc.edu.mx"), "Dashboard");

        // 2. Equipo + Ficha inicial. El formulario ya no ofrece Catrina; un campo HaceCatrina posteado se ignora.
        var crearHtml = await organizador.GetStringAsync("/Registro/CrearEquipo");
        Assert.DoesNotContain("Catrina", crearHtml);
        AssertRedirect(await PostAsync(organizador, "/Registro/CrearEquipo", "/Registro/CrearEquipo",
            ("Nombre", $"Equipo Flujo {sufijo}"), ("NombreAltar", "Altar del flujo"), ("CarreraId", carreraId.ToString()),
            ("MaestroEncargadoIdentificador", $"18{sufijo}"), ("UbicacionAltar", "Explanada"),
            ("DifuntoNombre", "Difunto del flujo"), ("DifuntoFechaDefuncion", "2020-01-15"), ("HaceCatrina", "true")), "Ficha");

        Equipo equipo;
        int integranteId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
            equipo = await context.EquiposConcurso.AsNoTracking().FirstAsync(e => e.Nombre == $"Equipo Flujo {sufijo}");
            integranteId = await context.Registrantes.Where(r => r.Identificador == $"1801{sufijo}").Select(r => r.Id).FirstAsync();
        }
        Assert.Matches(@"^ALT-\d{4}-[12]-\d{4,}$", equipo.CodigoPublico);
        Assert.False(equipo.HaceCatrina);

        // 3. Ficha: editar nombre no cambia el CodigoPublico; la ficha lo muestra y no ofrece Catrina.
        var fichaHtml = await organizador.GetStringAsync("/Registro/Ficha");
        Assert.Contains(equipo.CodigoPublico, fichaHtml);
        Assert.DoesNotContain("Catrina", fichaHtml);
        AssertRedirect(await PostAsync(organizador, "/Registro/Ficha", "/Registro/Ficha",
            ("EquipoId", equipo.Id.ToString()), ("Nombre", $"Equipo Flujo Renombrado {sufijo}"), ("NombreAltar", "Altar del flujo"),
            ("CarreraId", carreraId.ToString()), ("MaestroEncargadoIdentificador", $"18{sufijo}"), ("UbicacionAltar", "Explanada norte"),
            ("DifuntoNombre", "Difunto del flujo"), ("DifuntoFechaDefuncion", "2020-01-15"),
            ("CodigoPublico", "ALT-2099-1-9999")), "Ficha");

        // 4. Integrantes y Narrador.
        AssertRedirect(await PostAsync(organizador, "/Registro/Ficha", "/Registro/AgregarIntegrante", ("identificador", $"1801{sufijo}")), "");
        AssertRedirect(await PostAsync(organizador, "/Registro/Ficha", "/Registro/DesignarNarrador", ("registranteId", integranteId.ToString())), "");

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
            var recargado = await context.EquiposConcurso.AsNoTracking().Include(e => e.Integrantes).FirstAsync(e => e.Id == equipo.Id);
            Assert.Equal($"Equipo Flujo Renombrado {sufijo}", recargado.Nombre);
            Assert.Equal(equipo.CodigoPublico, recargado.CodigoPublico);
            Assert.Equal(2, recargado.Integrantes.Count);
            Assert.Single(recargado.Integrantes, i => i.Rol == RolEquipo.Narrador && i.RegistranteId == integranteId);

            // Flag legado encendido directamente en la BD (datos historicos): aun asi nada de Catrina aparece.
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE EquiposConcurso SET HaceCatrina = 1 WHERE Id = {equipo.Id}");
        }

        // 5. Evaluacion preliminar y final por un juez.
        await _factory.CrearJuezAsync($"juez-flujo-{sufijo}", "Juez-Flujo-2026!");
        var juez = _factory.CrearCliente();
        AssertRedirect(await PostAsync(juez, "/Registro/Login", "/Acceso/Login", ("usuario", $"juez-flujo-{sufijo}"), ("password", "Juez-Flujo-2026!")), "Historial");

        var evaluacionHtml = await juez.GetStringAsync($"/AltarEvaluacion/NuevaEvaluacion?equipoId={equipo.Id}");
        Assert.Contains("Evaluaci&oacute;n del altar", evaluacionHtml);
        Assert.DoesNotContain("Catrina", evaluacionHtml);
        Assert.DoesNotContain("tab-catrina", evaluacionHtml);

        var rutaEvaluacion = $"/AltarEvaluacion/NuevaEvaluacion?equipoId={equipo.Id}";
        // La pantalla lleva la version de la evaluacion abierta (C2); se lee de nuevo antes de cada guardado.
        async Task<(string, string)[]> Calificacion(string accion) => new[]
        {
            ("equipoId", equipo.Id.ToString()), ("niveles", "7"), ("tipoAltar", "Tradicional"), ("semblanza", "Gustaba del mar"),
            ("esenciaPersonalidad", "9"), ("valoracionGeneral", "8"), ("distribucionNiveles", "7"), ("narrador", "10"),
            // Campos de Catrina que una version vieja del formulario podria seguir enviando: se ignoran.
            ("vestuario", "10"), ("maquillaje", "10"), ("accesorios", "10"), ("originalidadCreatividad", "10"), ("apegoTradiciones", "10"),
            ("accion", accion),
            ("version", Regex.Match(await juez.GetStringAsync(rutaEvaluacion), "name=\"version\" value=\"([^\"]*)\"").Groups[1].Value)
        };
        AssertRedirect(await PostAsync(juez, rutaEvaluacion, "/AltarEvaluacion/GuardarEvaluacion", await Calificacion("preliminar")), "NuevaEvaluacion");
        AssertRedirect(await PostAsync(juez, rutaEvaluacion, "/AltarEvaluacion/GuardarEvaluacion", await Calificacion("final")), "Detalle");

        Evaluacion evaluacion;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
            evaluacion = await context.EvaluacionesConcurso.AsNoTracking().FirstAsync(e => e.EquipoId == equipo.Id);
            Assert.Equal(EstadoEvaluacion.Final, evaluacion.Estado);
            Assert.False(evaluacion.IncluyeCatrina);
            Assert.NotNull(evaluacion.NotaFinal);
            // 6. Ranking del altar (unico Final de su carrera en este periodo de pruebas o compite con otros: hay lugar).
            Assert.NotNull(evaluacion.Lugar);
            Assert.False(await context.EvaluacionesCatrina.AnyAsync(c => c.EvaluacionId == evaluacion.Id));
        }

        var detalleHtml = await juez.GetStringAsync($"/AltarEvaluacion/Detalle/{evaluacion.Id}");
        Assert.DoesNotContain("Catrina", detalleHtml);

        // 7. Constancias: juez (grupal, individuales, maestro, juez) y participante (la propia).
        await AssertPdfAsync(juez, $"/Constancia/DescargarGrupal?evaluacionId={evaluacion.Id}");
        await AssertPdfAsync(juez, $"/Constancia/DescargarMaestro?evaluacionId={evaluacion.Id}");
        await AssertPdfAsync(juez, $"/Constancia/DescargarJuez?evaluacionId={evaluacion.Id}");
        var zip = await juez.GetAsync($"/Constancia/DescargarIndividuales?evaluacionId={evaluacion.Id}");
        Assert.Equal(HttpStatusCode.OK, zip.StatusCode);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(await zip.Content.ReadAsByteArrayAsync(), 0, 2));
        await AssertPdfAsync(integrante, "/Constancia/DescargarMia");

        // 8. Administracion: configuracion, equipos y reporte de cierre sin Catrina.
        await _factory.CrearJuezAsync($"admin-flujo-{sufijo}", "Admin-Flujo-2026!", rol: "Admin");
        var admin = _factory.CrearCliente();
        AssertRedirect(await PostAsync(admin, "/Registro/Login", "/Acceso/Login", ("usuario", $"admin-flujo-{sufijo}"), ("password", "Admin-Flujo-2026!")), "Historial");

        Assert.DoesNotContain("Catrina", await admin.GetStringAsync("/AltarAdmin/Configuracion"));
        var equiposHtml = await admin.GetStringAsync("/AltarAdmin/RegistrantesYEquipos?tab=equipos");
        Assert.Contains(equipo.CodigoPublico, equiposHtml);
        Assert.DoesNotContain("Catrina", await admin.GetStringAsync($"/AltarAdmin/EditarEquipo/{equipo.Id}"));
        var reporteHtml = await admin.GetStringAsync("/AltarAdmin/ReportePeriodo");
        Assert.Contains("Podio del altar por carrera", reporteHtml);
        Assert.DoesNotContain("Catrina", reporteHtml);
        await AssertPdfAsync(admin, "/AltarAdmin/ReportePeriodoPdf");
    }

    // Guardar la configuracion ya no modifica los pesos de Catrina (columnas legadas intactas).
    [Fact]
    public async Task Configuracion_NoModificaPesosDeCatrina()
    {
        await _factory.CrearJuezAsync("admin-config-f2", "Admin-Config-2026!", rol: "Admin");
        var admin = _factory.CrearCliente();
        AssertRedirect(await PostAsync(admin, "/Registro/Login", "/Acceso/Login", ("usuario", "admin-config-f2"), ("password", "Admin-Config-2026!")), "Historial");

        var guardado = await PostAsync(admin, "/AltarAdmin/Configuracion", "/AltarAdmin/Configuracion",
            ("PesoObjetivoCulturalPct", "30"), ("PesoEsenciaPersonalidadPct", "30"), ("PesoValoracionGeneralPct", "20"),
            ("PesoDistribucionNivelesPct", "10"), ("PesoNarradorPct", "10"), ("UmbralAgrupacionDemografica", "5"),
            ("PesoCatrinaVestuarioPct", "99"), ("PesoCatrinaMaquillajePct", "1"));
        AssertRedirect(guardado, "Configuracion");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
        var periodo = Concurso.Core.Fechas.PeriodoHelper.ObtenerPeriodoActual();
        var config = await context.ConfiguracionesPeriodo.AsNoTracking().FirstAsync(c => c.Periodo == periodo);
        Assert.Equal(0.30m, config.PesoObjetivoCultural);
        Assert.Equal(0.25m, config.PesoCatrinaVestuario);
        Assert.Equal(0.20m, config.PesoCatrinaMaquillaje);
    }

    private static async Task AssertPdfAsync(HttpClient cliente, string ruta)
    {
        var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(await respuesta.Content.ReadAsByteArrayAsync(), 0, 4));
    }
}
