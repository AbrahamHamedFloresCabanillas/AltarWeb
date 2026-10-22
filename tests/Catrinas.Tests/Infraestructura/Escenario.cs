using Catrinas.Web.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Catrinas.Tests.Infraestructura;

// Construye por HTTP una inscripcion lista para evaluar: responsable + integrante (que personifica),
// maestro encargado opcional, en la carrera indicada.
public sealed class Escenario
{
    private static int _contador = 1000;

    public HttpClient Responsable { get; private init; } = null!;
    public HttpClient Integrante { get; private init; } = null!;
    public int InscripcionId { get; private init; }
    public int IntegranteId { get; private init; }
    public int ResponsableId { get; private init; }
    public string Nombre { get; private init; } = string.Empty;

    public static int Siguiente() => Interlocked.Increment(ref _contador);

    public static async Task<Escenario> CrearAsync(CatrinasFactory factory, int carreraId = 1, bool conPersonificador = true,
        bool conMaestro = false, string? nombre = null, params (string, string)[] extraInscripcion)
    {
        var n = Siguiente();
        var responsable = factory.Cliente();
        var integrante = factory.Cliente();
        Http.Redirige(await Http.SignupAsync(responsable, $"Responsable Catrina {n}", $"17{n:D5}", $"resp.cat{n}@uabc.edu.mx"), "Panel");
        Http.Redirige(await Http.SignupAsync(integrante, $"Integrante Catrina {n}", $"16{n:D5}", $"int.cat{n}@uabc.edu.mx"), "Panel");

        var extra = extraInscripcion.ToList();
        if (conMaestro)
        {
            var maestro = factory.Cliente();
            var idMaestro = $"5{n % 10000:D4}";
            Http.Redirige(await Http.SignupAsync(maestro, $"Maestra Catrina {n}", idMaestro, $"maestra.cat{n}@uabc.edu.mx"), "Panel");
            extra.Add(("MaestroEncargadoIdentificador", idMaestro));
        }

        nombre ??= $"Catrina de prueba {n}";
        Http.Redirige(await Http.CrearInscripcionAsync(responsable, nombre, carreraId, extra.ToArray()), "Inscripcion/Editar");

        var (inscripcionId, responsableId, integranteId) = factory.ConContexto(c =>
        {
            var i = c.Inscripciones.Single(x => x.Nombre == nombre);
            return (i.Id, i.ResponsableId, c.Registrantes.Single(r => r.CorreoInstitucional == $"int.cat{n}@uabc.edu.mx").Id);
        });

        // A1: el responsable invita y el integrante acepta desde su panel.
        Http.Redirige(await Http.PostDesdeAsync(responsable, "/Inscripcion/Editar", "/Inscripcion/AgregarIntegrante", ("identificador", $"16{n:D5}")), "Editar");
        var invitacionId = factory.ConContexto(c => c.Invitaciones.Single(x => x.InscripcionId == inscripcionId && x.RegistranteId == integranteId).Id);
        Http.Redirige(await Http.PostDesdeAsync(integrante, "/Registro/Panel", "/Inscripcion/AceptarInvitacion", ("invitacionId", invitacionId.ToString())), "Panel");
        if (conPersonificador)
        {
            Http.Redirige(await Http.PostDesdeAsync(responsable, "/Inscripcion/Editar", "/Inscripcion/DesignarPersonificador", ("registranteId", integranteId.ToString())), "Editar");
        }

        return new Escenario
        {
            Responsable = responsable,
            Integrante = integrante,
            InscripcionId = inscripcionId,
            IntegranteId = integranteId,
            ResponsableId = responsableId,
            Nombre = nombre
        };
    }

    public static async Task<HttpClient> JuradoAsync(CatrinasFactory factory, bool admin = false)
    {
        var usuario = $"jurado-{Siguiente()}";
        await factory.CrearJuezAsync(usuario, "Jurado-Pruebas-2026!", admin);
        var cliente = factory.Cliente();
        Http.Redirige(await Http.LoginJuradoAsync(cliente, usuario, "Jurado-Pruebas-2026!"), "Evaluacion");
        return cliente;
    }

    public static EvaluacionCatrina? Evaluacion(CatrinasFactory factory, int inscripcionId) =>
        factory.ConContexto(c => c.Evaluaciones.AsNoTracking().Include(e => e.Integrantes).FirstOrDefault(e => e.InscripcionId == inscripcionId));
}
