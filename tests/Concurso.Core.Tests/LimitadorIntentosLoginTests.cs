using Concurso.Core.Seguridad;
using Microsoft.Extensions.Options;

namespace Concurso.Core.Tests;

public class LimitadorIntentosLoginTests
{
    private sealed class RelojManual : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private sealed class OpcionesFijas : IOptionsMonitor<LimitesLoginOptions>
    {
        public OpcionesFijas(LimitesLoginOptions valor) => CurrentValue = valor;
        public LimitesLoginOptions CurrentValue { get; }
        public LimitesLoginOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<LimitesLoginOptions, string?> listener) => null;
    }

    private static (LimitadorIntentosLogin Limitador, RelojManual Reloj) Crear(int maxFallidos = 5, int ventanaMinutos = 5, int longitudMaxima = 128)
    {
        var reloj = new RelojManual();
        var opciones = new LimitesLoginOptions
        {
            CuentaMaxFallidos = maxFallidos,
            CuentaVentanaMinutos = ventanaMinutos,
            LongitudMaximaCuenta = longitudMaxima
        };
        return (new LimitadorIntentosLogin(new OpcionesFijas(opciones), reloj), reloj);
    }

    [Fact]
    public void Normaliza_TrimMinusculasYLongitudMaxima()
    {
        var (limitador, _) = Crear(longitudMaxima: 10);

        Assert.Equal("juan@uabc.", limitador.NormalizarCuenta("  Juan@UABC.edu.mx "));
        Assert.Equal("(vacia)", limitador.NormalizarCuenta("   "));
        Assert.Equal("(vacia)", limitador.NormalizarCuenta(null));
        Assert.Equal(10, limitador.NormalizarCuenta(new string('a', 100_000)).Length);
    }

    [Fact]
    public void BloqueaTrasMaxFallidos_ConRetryAfterDentroDeLaVentana()
    {
        var (limitador, reloj) = Crear(maxFallidos: 5, ventanaMinutos: 5);

        for (var i = 0; i < 4; i++) limitador.RegistrarFallo("juez", "victima");
        Assert.False(limitador.EstaBloqueada("juez", "victima", out _));

        limitador.RegistrarFallo("juez", "victima");
        reloj.Ahora = reloj.Ahora.AddMinutes(1);
        Assert.True(limitador.EstaBloqueada("juez", "victima", out var espera));
        Assert.Equal(TimeSpan.FromMinutes(4), espera);
    }

    [Fact]
    public void VariantesDeEscrituraCompartenContador()
    {
        var (limitador, _) = Crear(maxFallidos: 2);

        limitador.RegistrarFallo("registrante", "Ana@uabc.edu.mx");
        limitador.RegistrarFallo("registrante", "  ana@UABC.EDU.MX ");

        Assert.True(limitador.EstaBloqueada("registrante", "ana@uabc.edu.mx", out _));
    }

    [Fact]
    public void OtraCuentaYOtroAmbito_NoSeAfectan()
    {
        var (limitador, _) = Crear(maxFallidos: 1);

        limitador.RegistrarFallo("juez", "victima");

        Assert.True(limitador.EstaBloqueada("juez", "victima", out _));
        Assert.False(limitador.EstaBloqueada("juez", "otra", out _));
        Assert.False(limitador.EstaBloqueada("registrante", "victima", out _));
    }

    [Fact]
    public void AlTerminarLaVentana_SeDesbloquea()
    {
        var (limitador, reloj) = Crear(maxFallidos: 1, ventanaMinutos: 5);

        limitador.RegistrarFallo("juez", "victima");
        reloj.Ahora = reloj.Ahora.AddMinutes(5);

        Assert.False(limitador.EstaBloqueada("juez", "victima", out _));
    }

    [Fact]
    public void LoginExitoso_ReiniciaElContador()
    {
        var (limitador, _) = Crear(maxFallidos: 2);

        limitador.RegistrarFallo("juez", "usuario");
        limitador.Reiniciar("juez", "usuario");
        limitador.RegistrarFallo("juez", "usuario");

        Assert.False(limitador.EstaBloqueada("juez", "usuario", out _));
    }
}
