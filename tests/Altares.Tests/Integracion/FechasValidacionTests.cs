using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using AltarWeb.Models.Registro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Altares.Tests.Integracion;

// A2 y M3 (pruebas adversariales 2026-10-21).
// A2: la regla "al menos 1 año de fallecido" no se aplicaba si el periodo no tenia registro de configuracion,
//     aunque la Ficha le decia al participante que si (valor por defecto true); y no habia limite inferior
//     (se acepto el año 0001).
// M3: las fechas limite de la configuracion aceptaban el año 0009 y requisitos antes que inscripcion.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class FechasValidacionTests
{
    private const string Password = "Fechas-Validacion-2026!";
    private static int _contador = 4000;
    private readonly AltarWebFactory _factory;
    public FechasValidacionTests(AltarWebFactory factory) => _factory = factory;

    private static string Periodo => Concurso.Core.Fechas.PeriodoHelper.ObtenerPeriodoActual();

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rutaFormulario, string accion, params (string, string)[] campos)
    {
        var html = await cliente.GetStringAsync(rutaFormulario);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(token.Success, $"Sin token antiforgery en {rutaFormulario}");
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = token.Groups[1].Value;
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }

    private async Task<T> ConContextoAsync<T>(Func<AltarDbContext, Task<T>> accion)
    {
        using var scope = _factory.Services.CreateScope();
        return await accion(scope.ServiceProvider.GetRequiredService<AltarDbContext>());
    }

    // Ejecuta la prueba con la configuracion del periodo actual en el estado pedido y la restaura al final.
    private async Task ConConfiguracionAsync(bool existe, Func<Task> prueba)
    {
        var original = await ConContextoAsync(c => c.ConfiguracionesPeriodo.AsNoTracking().FirstOrDefaultAsync(x => x.Periodo == Periodo));
        await ConContextoAsync(async c =>
        {
            var actual = await c.ConfiguracionesPeriodo.FirstOrDefaultAsync(x => x.Periodo == Periodo);
            if (!existe && actual != null) c.ConfiguracionesPeriodo.Remove(actual);
            if (existe && actual == null) c.ConfiguracionesPeriodo.Add(new ConfiguracionPeriodo { Periodo = Periodo });
            return await c.SaveChangesAsync();
        });
        try
        {
            await prueba();
        }
        finally
        {
            await ConContextoAsync(async c =>
            {
                var actual = await c.ConfiguracionesPeriodo.FirstOrDefaultAsync(x => x.Periodo == Periodo);
                if (actual != null) c.ConfiguracionesPeriodo.Remove(actual);
                await c.SaveChangesAsync();
                if (original != null) { original.Id = 0; c.ConfiguracionesPeriodo.Add(original); }
                return await c.SaveChangesAsync();
            });
        }
    }

    private async Task<HttpClient> ParticipanteAsync()
    {
        var n = Interlocked.Increment(ref _contador);
        var cliente = _factory.CrearCliente();
        var r = await PostAsync(cliente, "/Registro/Signup", "/Registro/Signup",
            ("NombreCompleto", $"Participante Fechas {n}"), ("Identificador", $"16{n:D5}"), ("CorreoInstitucional", $"fechas.{n}@uabc.edu.mx"),
            ("Password", Password), ("ConfirmarPassword", Password));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        return cliente;
    }

    private async Task<(HttpResponseMessage Respuesta, string Nombre)> CrearEquipoAsync(HttpClient participante, string fechaDefuncion)
    {
        var nombre = $"Equipo Fechas {Interlocked.Increment(ref _contador)}";
        var carrera = await ConContextoAsync(c => c.CatalogoCarreras.Where(x => x.Activo).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync());
        var r = await PostAsync(participante, "/Registro/CrearEquipo", "/Registro/CrearEquipo",
            ("Nombre", nombre), ("NombreAltar", "Altar de fechas"), ("CarreraId", carrera.ToString()), ("MaestroEncargadoIdentificador", "54321"),
            ("DifuntoNombre", "Difunto de fechas"), ("DifuntoFechaDefuncion", fechaDefuncion));
        return (r, nombre);
    }

    private Task<bool> ExisteEquipoAsync(string nombre) => ConContextoAsync(c => c.EquiposConcurso.AnyAsync(e => e.Nombre == nombre));

    private static string HaceMeses(int meses) => DateTime.Today.AddMonths(-meses).ToString("yyyy-MM-dd");

    [Theory]
    [InlineData(false, "2030-01-01")]
    [InlineData(false, null)]          // hace dos meses
    [InlineData(true, "0001-01-01")]
    [InlineData(false, "0001-01-01")]
    public Task CrearEquipo_FechaDefuncionInvalida_SeRechaza(bool conConfiguracion, string? fecha) =>
        ConConfiguracionAsync(conConfiguracion, async () =>
        {
            var participante = await ParticipanteAsync();
            var (respuesta, nombre) = await CrearEquipoAsync(participante, fecha ?? HaceMeses(2));

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.False(await ExisteEquipoAsync(nombre));
        });

    [Fact]
    public Task CrearEquipo_FechaDeHaceDosAnios_SinConfiguracion_SeAcepta() =>
        ConConfiguracionAsync(false, async () =>
        {
            var participante = await ParticipanteAsync();
            var (respuesta, nombre) = await CrearEquipoAsync(participante, DateTime.Today.AddYears(-2).ToString("yyyy-MM-dd"));

            Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            Assert.True(await ExisteEquipoAsync(nombre));
        });

    private async Task<HttpClient> AdminAsync()
    {
        var usuario = $"admin-fechas-{Interlocked.Increment(ref _contador)}";
        await _factory.CrearJuezAsync(usuario, Password, rol: "Admin");
        var admin = _factory.CrearCliente();
        await PostAsync(admin, "/Registro/Login", "/Acceso/Login", ("usuario", usuario), ("password", Password));
        return admin;
    }

    [Theory]
    [InlineData("2030-01-01")]
    [InlineData("0001-01-01")]
    public Task AdminEditarEquipo_FechaDefuncionInvalida_SinConfiguracion_SeRechaza(string fecha) =>
        ConConfiguracionAsync(false, async () =>
        {
            var participante = await ParticipanteAsync();
            var (creado, nombre) = await CrearEquipoAsync(participante, "2020-01-15");
            Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
            var equipo = await ConContextoAsync(c => c.EquiposConcurso.SingleAsync(e => e.Nombre == nombre));
            var admin = await AdminAsync();

            var respuesta = await PostAsync(admin, $"/AltarAdmin/EditarEquipo/{equipo.Id}", "/AltarAdmin/EditarEquipo",
                ("Id", equipo.Id.ToString()), ("Nombre", nombre), ("NombreAltar", "Altar de fechas"), ("CarreraId", equipo.CarreraId.ToString()),
                ("MaestroEncargadoIdentificador", "54321"), ("DifuntoNombre", "Difunto de fechas"), ("DifuntoFechaDefuncion", fecha));

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var guardada = await ConContextoAsync(c => c.Difuntos.Where(d => d.EquipoId == equipo.Id).Select(d => d.FechaDefuncion).SingleAsync());
            Assert.Equal(new DateOnly(2020, 1, 15), guardada);
        });

    private static (string, string)[] Configuracion(string? inscripcion, string? requisitos) => new[]
    {
        ("FechaLimiteInscripcion", inscripcion ?? string.Empty), ("FechaLimiteRequisitos", requisitos ?? string.Empty),
        ("PesoObjetivoCulturalPct", "30"), ("PesoEsenciaPersonalidadPct", "30"), ("PesoValoracionGeneralPct", "20"),
        ("PesoDistribucionNivelesPct", "10"), ("PesoNarradorPct", "10"), ("UmbralAgrupacionDemografica", "5")
    };

    [Theory]
    [InlineData("0009-09-09", null)]
    [InlineData(null, "0009-09-09")]
    [InlineData("2210-01-01", null)]
    [InlineData("2026-11-01", "2026-10-25")] // requisitos antes que inscripcion
    public Task Configuracion_FechasAbsurdas_SeRechazan(string? inscripcion, string? requisitos) =>
        ConConfiguracionAsync(true, async () =>
        {
            var admin = await AdminAsync();
            var respuesta = await PostAsync(admin, "/AltarAdmin/Configuracion", "/AltarAdmin/Configuracion", Configuracion(inscripcion, requisitos));

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var config = await ConContextoAsync(c => c.ConfiguracionesPeriodo.AsNoTracking().SingleAsync(x => x.Periodo == Periodo));
            Assert.Null(config.FechaLimiteInscripcion);
            Assert.Null(config.FechaLimiteRequisitos);
        });

    [Fact]
    public Task Configuracion_FechasCoherentes_SeGuardan() =>
        ConConfiguracionAsync(true, async () =>
        {
            var admin = await AdminAsync();
            var respuesta = await PostAsync(admin, "/AltarAdmin/Configuracion", "/AltarAdmin/Configuracion", Configuracion("2026-10-25", "2026-10-25"));

            Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            var config = await ConContextoAsync(c => c.ConfiguracionesPeriodo.AsNoTracking().SingleAsync(x => x.Periodo == Periodo));
            Assert.Equal(new DateTime(2026, 10, 25), config.FechaLimiteRequisitos);
        });
}
