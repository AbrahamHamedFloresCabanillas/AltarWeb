using System.Security.Claims;
using System.Text.Json;
using Concurso.Core.Identidad;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Concurso.Core.Tests;

// SEC-01 (endurecimiento 2026-10-17): la identidad de Google solo se acepta con correo @uabc.edu.mx,
// email_verified = true y claim "hd" = uabc.edu.mx (organizacion de Google Workspace).
public class ValidacionIdentidadGoogleTests
{
    private static readonly ValidacionGoogleOptions PorDefecto = new();

    private static ClaimsPrincipal Principal(string? email, string? verificado = "True", string? hd = "uabc.edu.mx", string? nombre = "Ana Pérez")
    {
        var claims = new List<Claim>();
        if (email != null) claims.Add(new Claim(ClaimTypes.Email, email));
        if (nombre != null) claims.Add(new Claim(ClaimTypes.Name, nombre));
        if (verificado != null) claims.Add(new Claim(GoogleInstitucionalExtensions.ClaimEmailVerificado, verificado));
        if (hd != null) claims.Add(new Claim(GoogleInstitucionalExtensions.ClaimDominioWorkspace, hd));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Google"));
    }

    [Fact]
    public void CorreoUabcVerificadoConHd_EsValido()
    {
        var r = ValidacionIdentidadGoogle.Validar(Principal("ana.perez@uabc.edu.mx"), PorDefecto);

        Assert.True(r.Valida);
        Assert.Equal("ana.perez@uabc.edu.mx", r.Email);
        Assert.Equal("Ana Pérez", r.Nombre);
    }

    [Theory]
    [InlineData("False")]
    [InlineData("false")]
    [InlineData("")]
    [InlineData(null)]
    public void CorreoUabcNoVerificado_SeRechaza(string? verificado)
    {
        var r = ValidacionIdentidadGoogle.Validar(Principal("ana@uabc.edu.mx", verificado), PorDefecto);

        Assert.False(r.Valida);
        Assert.Equal(MotivoRechazoGoogle.CorreoNoVerificado, r.Motivo);
    }

    [Theory]
    [InlineData("ana@gmail.com")]
    [InlineData("ana@uabc.edu.mx.evil.com")]
    [InlineData("ana@uabcxedu.mx")]
    [InlineData("ana@sub.uabc.edu.mx")]
    public void CorreoDeOtroDominio_SeRechaza(string email)
    {
        var r = ValidacionIdentidadGoogle.Validar(Principal(email), PorDefecto);

        Assert.False(r.Valida);
        Assert.Equal(MotivoRechazoGoogle.DominioNoInstitucional, r.Motivo);
    }

    [Fact]
    public void SinCorreo_SeRechaza()
    {
        Assert.Equal(MotivoRechazoGoogle.SinCorreo, ValidacionIdentidadGoogle.Validar(Principal(null), PorDefecto).Motivo);
    }

    // Cuenta personal de Google creada con un correo @uabc.edu.mx (sin "hd") o de otra organizacion.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("otra-escuela.edu.mx")]
    [InlineData("uabc.edu.mx.evil.com")]
    public void HdAusenteOIncorrecto_SeRechaza(string? hd)
    {
        var r = ValidacionIdentidadGoogle.Validar(Principal("ana@uabc.edu.mx", hd: hd), PorDefecto);

        Assert.False(r.Valida);
        Assert.Equal(MotivoRechazoGoogle.DominioWorkspaceInvalido, r.Motivo);
    }

    // Si en F4 se demostrara que cuentas UABC legitimas no traen "hd", se puede relajar por configuracion;
    // email_verified y el dominio del correo se siguen exigiendo.
    [Fact]
    public void SinExigirHd_SigueExigiendoVerificacionYDominio()
    {
        var sinHd = new ValidacionGoogleOptions { ExigirDominioWorkspace = false };

        Assert.True(ValidacionIdentidadGoogle.Validar(Principal("ana@uabc.edu.mx", hd: null), sinHd).Valida);
        Assert.False(ValidacionIdentidadGoogle.Validar(Principal("ana@uabc.edu.mx", "false", hd: null), sinHd).Valida);
        Assert.False(ValidacionIdentidadGoogle.Validar(Principal("ana@gmail.com", hd: null), sinHd).Valida);
    }

    [Fact]
    public void CasingYEspacios_SeNormalizan()
    {
        var r = ValidacionIdentidadGoogle.Validar(Principal("  Ana.Perez@UABC.EDU.MX ", " TRUE ", " UABC.edu.mx "), PorDefecto);

        Assert.True(r.Valida);
        Assert.Equal("ana.perez@uabc.edu.mx", r.Email);
    }

    [Fact]
    public void SinNombre_UsaElCorreo()
    {
        Assert.Equal("ana@uabc.edu.mx", ValidacionIdentidadGoogle.Validar(Principal("ana@uabc.edu.mx", nombre: null), PorDefecto).Nombre);
    }
}

// Verifica que el handler de Google realmente convierta email_verified/hd de la respuesta de userinfo
// en claims (si no, la validacion rechazaria a todos o, peor, nunca veria el valor real).
public class MapeoClaimsGoogleTests
{
    private static (GoogleOptions Google, ValidacionGoogleOptions Validacion) Configurar(Dictionary<string, string?>? extra = null)
    {
        var valores = new Dictionary<string, string?>
        {
            ["GoogleAuth:ClientId"] = "id",
            ["GoogleAuth:ClientSecret"] = "secreto"
        };
        foreach (var (k, v) in extra ?? new()) valores[k] = v;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication().AddGoogleInstitucional(configuration);
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get("Google"),
            provider.GetRequiredService<IOptions<ValidacionGoogleOptions>>().Value);
    }

    private static ClaimsIdentity Aplicar(GoogleOptions options, string userinfoJson)
    {
        var identidad = new ClaimsIdentity("Google");
        using var json = JsonDocument.Parse(userinfoJson);
        foreach (var accion in options.ClaimActions) accion.Run(json.RootElement, identidad, "Google");
        return identidad;
    }

    [Fact]
    public void UserinfoDeCuentaUabc_ProduceClaimsQueLaValidacionAcepta()
    {
        var (google, validacion) = Configurar();
        var identidad = Aplicar(google, """
            {"sub":"123","name":"Ana Pérez","email":"ana@uabc.edu.mx","email_verified":true,"hd":"uabc.edu.mx"}
            """);

        Assert.True(ValidacionIdentidadGoogle.Validar(new ClaimsPrincipal(identidad), validacion).Valida);
    }

    [Fact]
    public void UserinfoNoVerificadoOSinHd_LaValidacionLoRechaza()
    {
        var (google, validacion) = Configurar();

        var noVerificado = Aplicar(google, """{"sub":"1","email":"ana@uabc.edu.mx","email_verified":false,"hd":"uabc.edu.mx"}""");
        var sinHd = Aplicar(google, """{"sub":"2","email":"ana@uabc.edu.mx","email_verified":true}""");

        Assert.Equal(MotivoRechazoGoogle.CorreoNoVerificado, ValidacionIdentidadGoogle.Validar(new ClaimsPrincipal(noVerificado), validacion).Motivo);
        Assert.Equal(MotivoRechazoGoogle.DominioWorkspaceInvalido, ValidacionIdentidadGoogle.Validar(new ClaimsPrincipal(sinHd), validacion).Motivo);
    }

    [Fact]
    public void ExigirHd_SeConfiguraSinRecompilar()
    {
        var (_, validacion) = Configurar(new() { ["GoogleAuth:ExigirDominioWorkspace"] = "false" });
        Assert.False(validacion.ExigirDominioWorkspace);
    }

    // Gate R7: el rechazo por falta de "hd" debe distinguirse en pantalla del de dominio ajeno,
    // sin incluir claims ni datos de la cuenta.
    [Fact]
    public void MensajeRechazo_DistingueFaltaDeHd()
    {
        var sinHd = ValidacionIdentidadGoogle.MensajeRechazo(MotivoRechazoGoogle.DominioWorkspaceInvalido);
        var ajeno = ValidacionIdentidadGoogle.MensajeRechazo(MotivoRechazoGoogle.DominioNoInstitucional);
        Assert.NotEqual(sinHd, ajeno);
        Assert.Contains("código: hd", sinHd);
        Assert.Equal("Debes usar tu correo institucional @uabc.edu.mx.", ajeno);
    }

    // Hallazgo F4: sin ClientId el sitio entero respondia 500 (validacion de opciones en cada peticion).
    // Ahora no arranca y el mensaje nombra la clave faltante, sin exponer ningun valor.
    [Theory]
    [InlineData(null, "secreto")]
    [InlineData("id", null)]
    [InlineData("", "")]
    public void SinCredencialesDeGoogle_NoArrancaYDiceQueFalta(string? clientId, string? clientSecret)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleAuth:ClientId"] = clientId,
            ["GoogleAuth:ClientSecret"] = clientSecret
        }).Build();

        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAuthentication().AddGoogleInstitucional(configuration));
        Assert.Contains("GoogleAuth:ClientId", error.Message);
    }
}
