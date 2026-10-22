using AltarWeb.Services;

namespace Altares.Tests.Unit;

// M2 (pruebas adversariales 2026-10-21): un nombre de 5,021 caracteres generaba dentro del ZIP de constancias
// un archivo que Windows no puede extraer. El nombre se limpia y ademas se recorta, conservando la extension.
public class NombreArchivoConstanciaTests
{
    [Fact]
    public void NombreLargo_SeRecorta_YConservaLaExtension()
    {
        var nombre = ConstanciaService.LimpiarNombreArchivo($"Constancia_{new string('A', 5000)}.pdf");

        Assert.True(nombre.Length <= ConstanciaService.LongitudMaximaNombreArchivo, $"Longitud {nombre.Length}");
        Assert.StartsWith("Constancia_AAA", nombre);
        Assert.EndsWith(".pdf", nombre);
    }

    [Fact]
    public void NombreNormal_SoloReemplazaCaracteresInvalidos()
    {
        Assert.Equal("Constancia_Ana_Maria.pdf", ConstanciaService.LimpiarNombreArchivo("Constancia_Ana/Maria.pdf"));
    }
}
