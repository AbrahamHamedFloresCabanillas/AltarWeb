using System.Net;
using System.Text.RegularExpressions;
using Catrinas.Tests.Infraestructura;

namespace Catrinas.Tests;

// B5 (revision manual del propietario, 2026-10-21): la interfaz decia "identificador" donde el usuario espera
// "Matrícula / No. de empleado" (7 digitos alumno, 5 maestro/administrativo).
[Collection(CatrinasCollection.Nombre)]
[Trait("Category", "Integracion")]
public class EtiquetaMatriculaTests
{
    private const string Etiqueta = "Matrícula / No. de empleado";
    private readonly CatrinasFactory _f;
    public EtiquetaMatriculaTests(CatrinasFactory f) => _f = f;

    // Texto que ve el usuario: sin etiquetas, atributos ni scripts (name="identificador" no cuenta).
    private static string TextoVisible(string html) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<script[\\s\\S]*?</script>", " "), "<[^>]+>", " "));

    private static void AssertSinIdentificador(string html, bool conEtiqueta = true)
    {
        var texto = TextoVisible(html);
        Assert.DoesNotMatch("(?i)identificador", texto);
        Assert.DoesNotMatch("(?i)(placeholder|onsubmit)=\"[^\"]*identificador ", html);
        // La etiqueta puede estar en el texto o, en los campos sin etiqueta propia, en el placeholder.
        if (conEtiqueta) Assert.True(texto.Contains(Etiqueta) || WebUtility.HtmlDecode(html).Contains($"placeholder=\"{Etiqueta}"), $"Falta \"{Etiqueta}\"");
    }

    [Fact]
    public async Task PantallasDelParticipanteYDeAdministracion_DicenMatricula()
    {
        var anonimo = _f.Cliente();
        AssertSinIdentificador(await anonimo.GetStringAsync("/Registro/Signup"));
        AssertSinIdentificador(await anonimo.GetStringAsync("/Registro/SolicitarJurado"));

        var e = await Escenario.CrearAsync(_f, carreraId: 7);
        AssertSinIdentificador(await e.Responsable.GetStringAsync("/Inscripcion/Editar"));
        AssertSinIdentificador(await e.Integrante.GetStringAsync("/Registro/Panel"), conEtiqueta: false);

        var admin = await Escenario.JuradoAsync(_f, admin: true);
        AssertSinIdentificador(await admin.GetStringAsync("/Admin/Participantes"));
    }
}
