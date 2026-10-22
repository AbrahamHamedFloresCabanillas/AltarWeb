using System.Net;
using System.Text.RegularExpressions;
using AltarWeb.Models;
using AltarWeb.Models.Registro;
using AltarWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Equipo = AltarWeb.Models.Registro.Equipo;
using Evaluacion = AltarWeb.Models.Registro.Evaluacion;

namespace Altares.Tests.Integracion;

// C3 (pruebas adversariales 2026-10-21): el admin movia de carrera a un equipo ya cerrado como Final y los
// lugares no se recalculaban: el equipo conservaba su "1º" en la carrera nueva y la de origen se quedaba sin
// primer lugar. Ahora EditarEquipo recalcula las dos carreras.
[Collection(AppCollection.Nombre)]
[Trait("Category", "Integracion")]
public class CambioCarreraLugaresTests
{
    // Periodo propio para que los lugares no compitan con equipos de otras pruebas.
    private const string Periodo = "2034-1";
    private readonly AltarWebFactory _factory;
    public CambioCarreraLugaresTests(AltarWebFactory factory) => _factory = factory;

    [Fact]
    public async Task MoverEquipoConFinal_RecalculaLugaresDeOrigenYDestino()
    {
        int origen, destino, movido, quedado, rival;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AltarDbContext>();
            var carreras = await context.CatalogoCarreras.Where(c => c.Activo).OrderBy(c => c.Id).Select(c => c.Id).Take(2).ToListAsync();
            (origen, destino) = (carreras[0], carreras[1]);

            async Task<int> EquipoConFinalAsync(string nombre, int carreraId, decimal nota)
            {
                var equipo = new Equipo { Nombre = nombre, NombreAltar = $"Altar {nombre}", CarreraId = carreraId, Periodo = Periodo, CodigoPublico = $"ALT-{Periodo}-{nombre}" };
                context.EquiposConcurso.Add(equipo);
                await context.SaveChangesAsync();
                context.EvaluacionesConcurso.Add(new Evaluacion { EquipoId = equipo.Id, Periodo = Periodo, Niveles = 7, NotaFinal = nota, Estado = EstadoEvaluacion.Final });
                await context.SaveChangesAsync();
                return equipo.Id;
            }

            movido = await EquipoConFinalAsync("C3-MOV", origen, 9.0m);
            quedado = await EquipoConFinalAsync("C3-QUE", origen, 8.0m);
            rival = await EquipoConFinalAsync("C3-RIV", destino, 9.5m);
            var calculo = new EvaluacionCalculoService(context);
            await calculo.RecalcularLugaresAsync(origen, Periodo);
            await calculo.RecalcularLugaresAsync(destino, Periodo);
        }
        Assert.Equal(new int?[] { 1, 2, 1 }, new[] { Lugar(movido), Lugar(quedado), Lugar(rival) });

        await _factory.CrearJuezAsync("admin-c3", "Admin-C3-2026!", rol: "Admin");
        var admin = _factory.CrearCliente();
        await PostAsync(admin, "/Registro/Login", "/Acceso/Login", ("usuario", "admin-c3"), ("password", "Admin-C3-2026!"));

        var respuesta = await PostAsync(admin, $"/AltarAdmin/EditarEquipo/{movido}", "/AltarAdmin/EditarEquipo",
            ("Id", movido.ToString()), ("Nombre", "C3-MOV"), ("NombreAltar", "Altar C3-MOV"), ("DifuntoNombre", "Difunto C3"),
            ("DifuntoFechaDefuncion", "2020-01-01"), ("CarreraId", destino.ToString()), ("MaestroEncargadoIdentificador", "54321"));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(1, Lugar(quedado)); // origen: el que quedo sube a 1º
        Assert.Equal(1, Lugar(rival));   // destino: 9.5 sigue primero
        Assert.Equal(2, Lugar(movido));  // destino: 9.0 queda segundo
    }

    private int? Lugar(int equipoId)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AltarDbContext>().EvaluacionesConcurso.AsNoTracking()
            .Single(e => e.EquipoId == equipoId).Lugar;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rutaFormulario, string accion, params (string, string)[] campos)
    {
        var html = await cliente.GetStringAsync(rutaFormulario);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(token.Success, $"Sin token antiforgery en {rutaFormulario}");
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = token.Groups[1].Value;
        return await cliente.PostAsync(accion, new FormUrlEncodedContent(datos));
    }
}
