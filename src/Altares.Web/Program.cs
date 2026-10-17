using AltarWeb.Models;
using AltarWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AltarDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AltarContext")
        ?? builder.Configuration.GetConnectionString("AltarWebContext")));

// Controles transversales de Concurso.Core (SEC-05 cookie de sesion, Data Protection).
builder.Services.AddConcursoSesion(builder.Configuration);
builder.Services.AddConcursoDataProtection(builder.Configuration);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddGoogleInstitucional(builder.Configuration);

builder.Services.AddScoped<ConstanciaService>();
builder.Services.AddScoped<EvaluacionCalculoService>();
builder.Services.AddScoped<ReportePeriodoService>();
builder.Services.AddScoped<IVerificadorCambioPassword, VerificadorCambioPasswordJuez>();
builder.Services.AddSingleton(sp => new ServicioCorreoSmtp(sp.GetRequiredService<IConfiguration>(), "Concurso Altares FIM"));

// SEC-04 (evolucion 2026-10-17, desde Concurso.Core): limite estricto de intentos FALLIDOS por cuenta
// (aplicado en AccesoController/RegistroController) + techo amplio por IP en los endpoints de login y de
// Google. Valores en "Seguridad:RateLimiting" (ver LimitesLoginOptions).
builder.Services.AddConcursoLimitesLogin(builder.Configuration);

// SEC-10 (antiforgery global) y SEC-03 (cambio de password obligatorio), desde Concurso.Core.
builder.Services.AddControllersWithViews(options =>
{
    // M2 (revision de seguridad F3): una cuenta dada de baja o desactivada pierde su sesion abierta.
    options.Filters.Add<RevalidarSesionFilter>();
    options.AgregarFiltrosSeguridad();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        SeedData.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrio un error al insertar el Juez inicial.");
    }

    // Fixup idempotente: las contrasenas de Jueces creadas antes de la migracion a PBKDF2 (SEC-02)
    // pueden estar en texto plano (muy legado) o en el SHA-256 sin salt anterior. Cualquier valor que
    // no coincida con ninguno de los dos formatos reconocidos por PasswordHashService se re-hashea
    // directo al formato nuevo (PBKDF2); los hashes SHA-256 legados existentes se migran mas adelante,
    // en el primer login exitoso de cada cuenta (rehash-on-login, ver AccesoController/RegistroController).
    try
    {
        var context = services.GetRequiredService<AltarDbContext>();
        var jueces = context.Jueces.IgnoreQueryFilters().ToList();
        var sinHashearReconocido = jueces.Where(j => !PasswordHashService.EsFormatoReconocido(j.Password)).ToList();
        foreach (var juez in sinHashearReconocido)
        {
            juez.Password = PasswordHashService.HashPassword(juez.Password);
        }
        if (sinHashearReconocido.Count > 0) context.SaveChanges();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrio un error al migrar contrasenas de Jueces a formato hash.");
    }
}

// CFG-02 (ForwardedHeaders), CFG-01 (manejo de errores + HSTS fuera de Development) y cabeceras de
// seguridad de SEC-05, desde Concurso.Core.
app.UseConcursoSeguridad("/Registro/Login");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Registro}/{action=Login}/{id?}");

app.Run();

// Expone la clase generada por las top-level statements para WebApplicationFactory<Program> en las
// pruebas de integracion (tests/Altares.Tests).
public partial class Program { }
