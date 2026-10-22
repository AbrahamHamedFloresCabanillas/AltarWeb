using System.Text.RegularExpressions;

namespace PruebaCarga;

// Cuentas ficticias de la prueba. Todas usan correos carga.*@uabc.edu.mx, identificadores 98xxxxx (alumnos) y
// 97xxx (maestros) y jueces "carga-juez-N": SOLO deben existir en bases de staging/demo y se borran con
// tools/PruebaCarga/limpiar-*.sql. Nunca correr contra la base de produccion.
public sealed record Cuenta(string Correo, string Identificador, string Nombre, int Equipo, bool Organizador);

public static class Cuentas
{
    public static Cuenta Organizador(int i) => new($"carga.a{i}@uabc.edu.mx", $"98{i:D5}", $"Carga Alumno A{i}", i, true);
    public static Cuenta Integrante(int i) => new($"carga.b{i}@uabc.edu.mx", $"98{50000 + i:D5}", $"Carga Alumno B{i}", i, false);
    public static Cuenta Maestro(int i) => new($"carga.m{i}@uabc.edu.mx", $"97{i:D3}", $"Carga Maestro M{i}", i, false);
    public static string Juez(int j) => $"carga-juez-{j}";
    public static string NombreEquipo(int i) => $"Carga {i:D3}";
}

// Lo que cambia entre Altares y Catrinas: rutas, nombres de campos y reglas de la ficha.
public interface IAppBajoPrueba
{
    string Nombre { get; }
    string RutaDiagnostico { get; }
    Task<bool> LoginParticipanteAsync(Cliente c, Cuenta cuenta, string password, string operacion = "login participante");
    Task<bool> LoginJuezAsync(Cliente c, string usuario, string password, string operacion = "login juez");
    Task PrepararEquipoAsync(Cliente organizador, Cliente integrante, Cliente? maestro, int i, string password);
    Task CrearJuezAsync(Cliente admin, string usuario, string password);
    Task<List<int>> ObjetivosJuezAsync(Cliente juez, IReadOnlyList<int> equipos);
    Task CicloParticipanteAsync(Cliente c, Cuenta cuenta, int iteracion);
    Task GuardarAsync(Cliente juez, int objetivo, bool final);
    Task CicloAdminAsync(Cliente admin);
    Task<List<int>> EvaluacionesFinalesAsync(Cliente admin);
    Task DescargasAsync(Cliente c, int evaluacionId);
}

public abstract class AppBase
{
    protected static readonly Regex OpcionCarrera = new("name=\"CarreraId\"[\\s\\S]*?<option value=\"([1-9]\\d*)\"");

    protected static async Task<string> CarreraAsync(Cliente c, string ruta)
    {
        var m = OpcionCarrera.Match(await c.HtmlAsync(ruta));
        if (!m.Success) throw new InvalidOperationException($"No se encontro el catalogo de carreras en {ruta}.");
        return m.Groups[1].Value;
    }

    protected static void Exigir(bool condicion, string que, string? cuerpo = null)
    {
        if (condicion) return;
        // Solo los mensajes de validacion de la pagina (datos ficticios), para saber que rechazo la app.
        var mensajes = cuerpo == null ? "" : string.Join(" | ", Regex.Matches(cuerpo, "class=\"[^\"]*(?:field-validation-error|validation-summary-errors|altar-alert)[^\"]*\"[^>]*>([\\s\\S]*?)</")
            .Select(m => Regex.Replace(m.Groups[1].Value, "<[^>]+>", " ").Trim()).Where(t => t.Length > 0));
        throw new InvalidOperationException("Preparacion fallida: " + que + (mensajes.Length > 0 ? " — " + mensajes : ""));
    }

    // El cambio de contraseña obligatorio se resuelve con la misma contraseña de la prueba.
    protected static async Task<bool> ResolverCambioObligatorioAsync(Cliente c, string? destino, string password)
    {
        if (destino?.Contains("CambiarPasswordObligatorio") != true) return true;
        var r = await c.PostAsync("/Acceso/CambiarPasswordObligatorio", "/Acceso/CambiarPasswordObligatorio", "cambio password",
            ("nuevaPassword", password), ("confirmarPassword", password));
        return r.Codigo == 302;
    }
}

public sealed class AppAltares : AppBase, IAppBajoPrueba
{
    private static readonly Regex Narrador = new("/Registro/DesignarNarrador\"[^>]*>\\s*<input type=\"hidden\" name=\"registranteId\" value=\"(\\d+)\"");
    private static readonly Regex EquipoBusqueda = new("NuevaEvaluacion\\?equipoId=(\\d+)");
    private static readonly Regex Invitacion = new("name=\"invitacionId\" value=\"(\\d+)\"");
    private static readonly Regex Detalle = new("/AltarEvaluacion/Detalle/(\\d+)");

    public string Nombre => "altares";
    public string RutaDiagnostico => "/AltarAdmin/Diagnostico";

    public async Task<bool> LoginParticipanteAsync(Cliente c, Cuenta cuenta, string password, string operacion)
    {
        var r = await c.PostAsync("/Registro/Login", "/Registro/Login", operacion,
            ("CorreoInstitucional", cuenta.Correo), ("Password", password));
        return r.Codigo == 302 && r.Destino?.Contains("Dashboard") == true;
    }

    public async Task<bool> LoginJuezAsync(Cliente c, string usuario, string password, string operacion)
    {
        var r = await c.PostAsync("/Registro/Login", "/Acceso/Login", operacion, ("usuario", usuario), ("password", password));
        // Un login fallido regresa a la pantalla de inicio (ruta por defecto) con el aviso en TempData.
        if (r.Codigo != 302 || r.Destino is null || r.Destino == "/" || r.Destino.StartsWith("/?")) return false;
        return await ResolverCambioObligatorioAsync(c, r.Destino, password);
    }

    public async Task PrepararEquipoAsync(Cliente org, Cliente integrante, Cliente? maestro, int i, string password)
    {
        foreach (var (cliente, cuenta) in new[] { (maestro!, Cuentas.Maestro(i)), (org, Cuentas.Organizador(i)), (integrante, Cuentas.Integrante(i)) })
        {
            var r = await cliente.PostAsync("/Registro/Signup", "/Registro/Signup", "preparar signup",
                ("NombreCompleto", cuenta.Nombre), ("Identificador", cuenta.Identificador), ("CorreoInstitucional", cuenta.Correo),
                ("Password", password), ("ConfirmarPassword", password), ("AceptoAvisoYTerminos", "true"));
            Exigir(r.Codigo == 302, $"signup de {cuenta.Correo} devolvio {r.Codigo}", r.Cuerpo);
        }

        var carrera = await CarreraAsync(org, "/Registro/CrearEquipo");
        var crear = await org.PostAsync("/Registro/CrearEquipo", "/Registro/CrearEquipo", "preparar crear equipo",
            ("Nombre", Cuentas.NombreEquipo(i)), ("NombreAltar", $"Altar de carga {i}"), ("CarreraId", carrera),
            ("MaestroEncargadoIdentificador", Cuentas.Maestro(i).Identificador), ("UbicacionAltar", "Explanada FIM"),
            ("DifuntoNombre", $"Difunto ficticio {i}"), ("DifuntoFechaDefuncion", "2020-01-01"));
        Exigir(crear.Destino?.Contains("Ficha") == true, $"crear equipo {i} devolvio {crear.Codigo}", crear.Cuerpo);

        var agregar = await org.PostAsync("/Registro/Ficha", "/Registro/AgregarIntegrante", "preparar agregar integrante",
            ("identificador", Cuentas.Integrante(i).Identificador));
        Exigir(agregar.Codigo == 302, $"agregar integrante al equipo {i}");

        // A1: agregar crea una invitacion; el integrante la acepta desde su portal.
        var invitacion = Invitacion.Match(await integrante.HtmlAsync("/Registro/Dashboard"));
        Exigir(invitacion.Success, $"no aparece la invitacion al equipo {i}");
        var aceptar = await integrante.PostAsync("/Registro/Dashboard", "/Registro/AceptarInvitacion", "preparar aceptar invitacion",
            ("invitacionId", invitacion.Groups[1].Value));
        Exigir(aceptar.Codigo == 302, $"aceptar invitacion al equipo {i}");

        var m = Narrador.Match(await org.HtmlAsync("/Registro/Ficha"));
        Exigir(m.Success, $"no aparece el boton de narrador del equipo {i}");
        var narrador = await org.PostAsync("/Registro/Ficha", "/Registro/DesignarNarrador", "preparar narrador", ("registranteId", m.Groups[1].Value));
        Exigir(narrador.Codigo == 302, $"designar narrador del equipo {i}");
    }

    public async Task CrearJuezAsync(Cliente admin, string usuario, string password)
    {
        var r = await admin.PostAsync("/AltarAdmin/CrearJuez", "/AltarAdmin/CrearJuez", "preparar crear juez",
            ("NombreCompleto", "Juez de carga " + usuario), ("Usuario", usuario), ("Password", password), ("Rol", "Juez"));
        Exigir(r.Codigo == 302, $"crear juez {usuario} devolvio {r.Codigo}");
    }

    // Busca cada equipo asignado por nombre (escenario "listado/busqueda") y devuelve sus ids.
    public async Task<List<int>> ObjetivosJuezAsync(Cliente juez, IReadOnlyList<int> equipos)
    {
        var ids = new List<int>();
        foreach (var i in equipos)
        {
            var r = await juez.GetAsync("/AltarEvaluacion/NuevaEvaluacion?q=" + Uri.EscapeDataString(Cuentas.NombreEquipo(i)), "busqueda equipos");
            var m = EquipoBusqueda.Match(r.Cuerpo);
            if (m.Success) ids.Add(int.Parse(m.Groups[1].Value));
        }
        return ids;
    }

    public async Task CicloParticipanteAsync(Cliente c, Cuenta cuenta, int iteracion)
    {
        await c.GetAsync("/Registro/Dashboard", "dashboard");
        if (!cuenta.Organizador) return;
        await c.GetAsync("/Registro/Ficha", "ficha (ver)");
        if (iteracion % 3 == 0)
        {
            // Edicion real de la ficha. Si el equipo ya tiene evaluacion la app la bloquea (302 a Dashboard), igual se mide.
            await c.PostAsync("/Registro/Ficha", "/Registro/Ficha", "ficha (guardar)",
                ("Nombre", Cuentas.NombreEquipo(cuenta.Equipo)), ("NombreAltar", $"Altar de carga {cuenta.Equipo}"),
                ("CarreraId", await CarreraAsync(c, "/Registro/Ficha")), ("MaestroEncargadoIdentificador", Cuentas.Maestro(cuenta.Equipo).Identificador),
                ("UbicacionAltar", $"Explanada FIM {iteracion}"), ("DifuntoNombre", $"Difunto ficticio {cuenta.Equipo}"), ("DifuntoFechaDefuncion", "2020-01-01"));
        }
    }

    public async Task GuardarAsync(Cliente juez, int equipoId, bool final)
    {
        var campos = new List<(string, string)>
        {
            ("equipoId", equipoId.ToString()), ("niveles", "7"), ("tipoAltar", "Tradicional"), ("semblanza", "Semblanza de prueba de carga"),
            ("esenciaPersonalidad", Util.D(Util.Azar.Next(5, 11))), ("valoracionGeneral", Util.D(Util.Azar.Next(5, 11))),
            ("distribucionNiveles", Util.D(Util.Azar.Next(5, 11))), ("narrador", Util.D(Util.Azar.Next(5, 11))),
            ("accion", final ? "final" : "preliminar"), ("confirmacion", final ? "CONFIRMO" : "")
        };
        var html = await juez.GetAsync($"/AltarEvaluacion/NuevaEvaluacion?equipoId={equipoId}", "evaluacion (abrir)");
        foreach (Match m in Regex.Matches(html.Cuerpo, "name=\"Satisfaccion_(\\d+)\""))
        {
            campos.Add(($"Satisfaccion_{m.Groups[1].Value}", Util.Azar.Next(3) switch { 0 => "NoLoTiene", 1 => "Regular", _ => "Bueno" }));
        }
        // Version de la evaluacion abierta (C2): sin ella el servidor rechaza el guardado de una Preliminar existente.
        campos.Add(("version", Regex.Match(html.Cuerpo, "name=\"version\" value=\"([^\"]*)\"").Groups[1].Value));
        await juez.PostAsync($"/AltarEvaluacion/NuevaEvaluacion?equipoId={equipoId}", "/AltarEvaluacion/GuardarEvaluacion",
            final ? "guardar Final" : "guardar Preliminar", campos.Distinct().ToArray());
    }

    public async Task CicloAdminAsync(Cliente admin)
    {
        await admin.GetAsync("/AltarEvaluacion/Historial", "admin historial");
        await admin.GetAsync("/AltarAdmin/RegistrantesYEquipos?tab=equipos", "admin listado equipos");
        await admin.GetAsync("/AltarAdmin/ReportePeriodo", "ranking / reporte");
    }

    public async Task<List<int>> EvaluacionesFinalesAsync(Cliente admin) =>
        Detalle.Matches(await admin.HtmlAsync("/AltarEvaluacion/Historial")).Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList();

    public async Task DescargasAsync(Cliente c, int evaluacionId)
    {
        await c.GetAsync($"/Constancia/DescargarGrupal?evaluacionId={evaluacionId}", "PDF constancia grupal", leerCuerpo: false);
        await c.GetAsync($"/Constancia/DescargarIndividuales?evaluacionId={evaluacionId}", "ZIP constancias individuales", leerCuerpo: false);
        await c.GetAsync($"/Constancia/DescargarJuez?evaluacionId={evaluacionId}", "PDF constancia juez", leerCuerpo: false);
        await c.GetAsync("/AltarAdmin/ReportePeriodoPdf", "PDF reporte de cierre", leerCuerpo: false);
    }
}

public sealed class AppCatrinas : AppBase, IAppBajoPrueba
{
    private static readonly Regex Personificador = new("/Inscripcion/DesignarPersonificador\"[^>]*>\\s*<input type=\"hidden\" name=\"registranteId\" value=\"(\\d+)\"");
    private static readonly Regex Evaluar = new("/Evaluacion/Evaluar\\?inscripcionId=(\\d+)");
    private static readonly Regex Detalle = new("/Evaluacion/Detalle/(\\d+)");
    private static readonly Regex RowVersion = new("name=\"rowVersion\" value=\"([^\"]*)\"");
    private static readonly Regex Invitacion = new("name=\"invitacionId\" value=\"(\\d+)\"");

    public string Nombre => "catrinas";
    public string RutaDiagnostico => "/Admin/Diagnostico";

    public async Task<bool> LoginParticipanteAsync(Cliente c, Cuenta cuenta, string password, string operacion)
    {
        var r = await c.PostAsync("/Registro/Login", "/Registro/Login", operacion,
            ("CorreoInstitucional", cuenta.Correo), ("Password", password));
        return r.Codigo == 302 && r.Destino?.Contains("Panel") == true;
    }

    public async Task<bool> LoginJuezAsync(Cliente c, string usuario, string password, string operacion)
    {
        var r = await c.PostAsync("/Registro/Login?tab=jurado", "/Acceso/Login", operacion, ("usuario", usuario), ("password", password));
        // Un login fallido regresa a la pantalla de inicio (ruta por defecto) con el aviso en TempData.
        if (r.Codigo != 302 || r.Destino is null || r.Destino == "/" || r.Destino.StartsWith("/?")) return false;
        return await ResolverCambioObligatorioAsync(c, r.Destino, password);
    }

    public async Task PrepararEquipoAsync(Cliente org, Cliente integrante, Cliente? maestro, int i, string password)
    {
        foreach (var (cliente, cuenta) in new[] { (org, Cuentas.Organizador(i)), (integrante, Cuentas.Integrante(i)) })
        {
            var r = await cliente.PostAsync("/Registro/Signup", "/Registro/Signup", "preparar signup",
                ("NombreCompleto", cuenta.Nombre), ("Identificador", cuenta.Identificador), ("CorreoInstitucional", cuenta.Correo),
                ("Password", password), ("ConfirmarPassword", password), ("AceptoAvisoYTerminos", "true"));
            Exigir(r.Codigo == 302, $"signup de {cuenta.Correo} devolvio {r.Codigo}", r.Cuerpo);
        }

        var carrera = await CarreraAsync(org, "/Inscripcion/Crear");
        var crear = await org.PostAsync("/Inscripcion/Crear", "/Inscripcion/Crear", "preparar crear inscripcion",
            ("Nombre", Cuentas.NombreEquipo(i)), ("CarreraId", carrera));
        Exigir(crear.Destino?.Contains("Editar") == true, $"crear inscripcion {i} devolvio {crear.Codigo}", crear.Cuerpo);

        var agregar = await org.PostAsync("/Inscripcion/Editar", "/Inscripcion/AgregarIntegrante", "preparar agregar integrante",
            ("identificador", Cuentas.Integrante(i).Identificador));
        Exigir(agregar.Codigo == 302, $"agregar integrante a la inscripcion {i}");

        // A1: agregar crea una invitacion; el integrante la acepta desde su panel.
        var invitacion = Invitacion.Match(await integrante.HtmlAsync("/Registro/Panel"));
        Exigir(invitacion.Success, $"no aparece la invitacion a la inscripcion {i}");
        var aceptar = await integrante.PostAsync("/Registro/Panel", "/Inscripcion/AceptarInvitacion", "preparar aceptar invitacion",
            ("invitacionId", invitacion.Groups[1].Value));
        Exigir(aceptar.Codigo == 302, $"aceptar invitacion a la inscripcion {i}");

        var m = Personificador.Match(await org.HtmlAsync("/Inscripcion/Editar"));
        Exigir(m.Success, $"no aparece el boton de personificador de la inscripcion {i}");
        var p = await org.PostAsync("/Inscripcion/Editar", "/Inscripcion/DesignarPersonificador", "preparar personificador", ("registranteId", m.Groups[1].Value));
        Exigir(p.Codigo == 302, $"designar personificador de la inscripcion {i}");
    }

    public async Task CrearJuezAsync(Cliente admin, string usuario, string password)
    {
        var r = await admin.PostAsync("/Admin/CrearJuez", "/Admin/CrearJuez", "preparar crear juez",
            ("NombreCompleto", "Juez de carga " + usuario), ("Usuario", usuario), ("Password", password), ("EsAdmin", "false"));
        Exigir(r.Codigo == 302, $"crear juez {usuario} devolvio {r.Codigo}");
    }

    // La lista del jurado muestra todas las inscripciones del periodo; cada juez toma las que le tocan.
    public async Task<List<int>> ObjetivosJuezAsync(Cliente juez, IReadOnlyList<int> equipos)
    {
        var r = await juez.GetAsync("/Evaluacion/Index", "busqueda equipos");
        var porNombre = new Dictionary<int, int>();
        foreach (var i in equipos)
        {
            var pos = r.Cuerpo.IndexOf(Cuentas.NombreEquipo(i), StringComparison.Ordinal);
            if (pos < 0) continue;
            var m = Evaluar.Match(r.Cuerpo, pos);
            if (m.Success) porNombre[i] = int.Parse(m.Groups[1].Value);
        }
        return porNombre.Values.ToList();
    }

    public async Task CicloParticipanteAsync(Cliente c, Cuenta cuenta, int iteracion)
    {
        await c.GetAsync("/Registro/Panel", "dashboard");
        if (!cuenta.Organizador) return;
        await c.GetAsync("/Inscripcion/Editar", "ficha (ver)");
        if (iteracion % 3 == 0)
        {
            await c.PostAsync("/Inscripcion/Editar", "/Inscripcion/Editar", "ficha (guardar)",
                ("Nombre", Cuentas.NombreEquipo(cuenta.Equipo)), ("CarreraId", await CarreraAsync(c, "/Inscripcion/Editar")));
        }
    }

    public async Task GuardarAsync(Cliente juez, int inscripcionId, bool final)
    {
        var html = await juez.GetAsync($"/Evaluacion/Evaluar?inscripcionId={inscripcionId}", "evaluacion (abrir)");
        var rv = RowVersion.Match(html.Cuerpo);
        await juez.PostAsync($"/Evaluacion/Evaluar?inscripcionId={inscripcionId}", "/Evaluacion/Guardar",
            final ? "guardar Final" : "guardar Preliminar",
            ("inscripcionId", inscripcionId.ToString()),
            ("vestuario", Util.D(Util.Azar.Next(5, 11))), ("maquillaje", Util.D(Util.Azar.Next(5, 11))),
            ("accesorios", Util.D(Util.Azar.Next(5, 11))), ("originalidadCreatividad", Util.D(Util.Azar.Next(5, 11))),
            ("apegoTradiciones", Util.D(Util.Azar.Next(5, 11))), ("accion", final ? "final" : "preliminar"),
            ("confirmacion", final ? "CONFIRMO" : ""), ("rowVersion", rv.Success ? System.Net.WebUtility.HtmlDecode(rv.Groups[1].Value) : ""));
    }

    public async Task CicloAdminAsync(Cliente admin)
    {
        await admin.GetAsync("/Evaluacion/Index", "admin historial");
        await admin.GetAsync("/Admin/Participantes", "admin listado equipos");
        await admin.GetAsync("/Evaluacion/Ranking", "ranking / reporte");
    }

    public async Task<List<int>> EvaluacionesFinalesAsync(Cliente admin) =>
        Detalle.Matches(await admin.HtmlAsync("/Evaluacion/Ranking")).Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList();

    public async Task DescargasAsync(Cliente c, int evaluacionId)
    {
        await c.GetAsync($"/Constancia/Grupal?evaluacionId={evaluacionId}", "PDF constancia grupal", leerCuerpo: false);
        await c.GetAsync($"/Constancia/Individuales?evaluacionId={evaluacionId}", "ZIP constancias individuales", leerCuerpo: false);
        await c.GetAsync($"/Constancia/Jurado?evaluacionId={evaluacionId}", "PDF constancia juez", leerCuerpo: false);
    }
}
