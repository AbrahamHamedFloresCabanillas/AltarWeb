using Concurso.Core.Pdf;

namespace Concurso.Core.Tests;

// Maquetacion comun de los manuales de usuario (2026-10-22): genera un PDF valido con el contenido que le pase la app.
public class ManualUsuarioPdfTests
{
    private static readonly ManualUsuario Manual = new("Concurso de Prueba", "Juez", "Introduccion de prueba.", new[]
    {
        new SeccionManual("Primer tema", "Texto del tema.", new[] { "Paso uno.", "Paso dos." }, new[] { "Aviso importante." }),
        new SeccionManual("Segundo tema", Pasos: new[] { "Unico paso." })
    });

    [Fact]
    public void Generar_DevuelveUnPdf()
    {
        var pdf = ManualUsuarioPdf.Generar(Manual, new DateTime(2026, 10, 22));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 5_000);
    }

    [Fact]
    public void NombreArchivo_IncluyeRolYConcurso()
    {
        Assert.Equal("Manual-Juez-Concurso-de-Prueba.pdf", Manual.NombreArchivo);
        Assert.Equal("Manual-Administracion-Concurso-de-Prueba.pdf", (Manual with { Rol = "Administración" }).NombreArchivo);
    }
}
