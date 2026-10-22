using System.Net;
using Catrinas.Tests.Infraestructura;

namespace Catrinas.Tests;

// A3 (pruebas adversariales 2026-10-21): cualquiera puede pausar 5 minutos la cuenta de otra persona con 5
// contraseñas incorrectas. Decision del propietario: los limites de login NO se relajan; la administracion ve que
// cuentas estan bloqueadas y puede desbloquearlas.
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class DesbloqueoCuentasTests
{
    private const string Password = "Desbloqueo-Catrinas-2026!";
    private readonly CatrinasFactory _f;
    public DesbloqueoCuentasTests(CatrinasFactory f) => _f = f;

    // Login enviado desde una IP concreta (cabecera de pruebas), para separar al atacante del dueño.
    private static async Task<HttpResponseMessage> LoginDesdeAsync(HttpClient cliente, string ip, string accion, params (string, string)[] campos)
    {
        var datos = campos.ToDictionary(c => c.Item1, c => c.Item2);
        datos["__RequestVerificationToken"] = await Http.TokenAsync(cliente);
        var mensaje = new HttpRequestMessage(HttpMethod.Post, accion) { Content = new FormUrlEncodedContent(datos) };
        mensaje.Headers.Add(CatrinasFactory.CabeceraIpRemota, ip);
        return await cliente.SendAsync(mensaje);
    }

    [Fact]
    public async Task JuradoBloqueadoPorTerceros_ElAdminLoVeYLoDesbloquea()
    {
        var usuario = $"jurado-saboteado-{Escenario.Siguiente()}";
        var id = await _f.CrearJuezAsync(usuario, Password);
        var atacante = _f.Cliente();
        for (var i = 0; i < 5; i++) await LoginDesdeAsync(atacante, "198.51.100.110", "/Acceso/Login", ("usuario", usuario), ("password", "mala"));
        var dueno = _f.Cliente();
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await LoginDesdeAsync(dueno, "198.51.100.120", "/Acceso/Login", ("usuario", usuario), ("password", Password))).StatusCode);

        var admin = await Escenario.JuradoAsync(_f, admin: true);
        Assert.Contains("Bloqueada", await admin.GetStringAsync("/Admin/Jurado"));
        Http.Redirige(await Http.PostDesdeAsync(admin, "/Admin/Jurado", "/Admin/DesbloquearJuez", ("id", id.ToString())), "Jurado");

        Http.Redirige(await LoginDesdeAsync(dueno, "198.51.100.120", "/Acceso/Login", ("usuario", usuario), ("password", Password)), "Evaluacion");
    }

    [Fact]
    public async Task ParticipanteBloqueadoPorTerceros_ElAdminLoVeYLoDesbloquea()
    {
        var n = Escenario.Siguiente();
        var correo = $"saboteado.cat{n}@uabc.edu.mx";
        Http.Redirige(await Http.SignupAsync(_f.Cliente(), "Participante Saboteado", $"13{n:D5}", correo, Password), "Panel");
        var atacante = _f.Cliente();
        for (var i = 0; i < 5; i++) await LoginDesdeAsync(atacante, "198.51.100.130", "/Registro/Login", ("CorreoInstitucional", correo), ("Password", "mala"));
        var dueno = _f.Cliente();
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await LoginDesdeAsync(dueno, "198.51.100.140", "/Registro/Login", ("CorreoInstitucional", correo), ("Password", Password))).StatusCode);

        var admin = await Escenario.JuradoAsync(_f, admin: true);
        Assert.Contains("Bloqueada", await admin.GetStringAsync("/Admin/Participantes"));
        var id = _f.ConContexto(c => c.Registrantes.Single(r => r.CorreoInstitucional == correo).Id);
        Http.Redirige(await Http.PostDesdeAsync(admin, "/Admin/Participantes", "/Admin/DesbloquearParticipante", ("id", id.ToString())), "Participantes");

        Http.Redirige(await LoginDesdeAsync(dueno, "198.51.100.140", "/Registro/Login", ("CorreoInstitucional", correo), ("Password", Password)), "Panel");
    }
}
