using System.Threading.RateLimiting;
using AltarWeb.Filters;
using AltarWeb.Models;
using AltarWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AltarDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AltarContext")
        ?? builder.Configuration.GetConnectionString("AltarWebContext")));

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    // SEC-05: la cookie de sesion es el portador real de identidad (JuezId/JuezRol/RegistranteId);
    // sin Secure viaja en claro ante un downgrade HTTP y puede ser secuestrada.
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddGoogle("Google", options =>
    {
        options.ClientId = builder.Configuration["GoogleAuth:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["GoogleAuth:ClientSecret"] ?? string.Empty;
        options.CallbackPath = "/Registro/google-callback";
        options.Scope.Add("email");
        options.Scope.Add("profile");
    });

builder.Services.AddScoped<ConstanciaService>();
builder.Services.AddScoped<EvaluacionCalculoService>();
builder.Services.AddScoped<ReportePeriodoService>();

// SEC-04: throttling por IP de las rutas de login (Acceso/Login, Registro/Login, GoogleCallback).
// No sustituye PBKDF2/rehash-on-login (SEC-02); es una capa adicional contra fuerza bruta online.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Redirect("/Registro/Login?error=" +
            Uri.EscapeDataString("Demasiados intentos. Espera un minuto e intenta de nuevo."));
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddControllersWithViews(options =>
{
    // SEC-10: valida el antiforgery token en todos los POST/PUT/DELETE. Todas las vistas del proyecto
    // usan tag helpers asp-action (emiten el token automaticamente) y no hay POSTs disparados por JS,
    // asi que no requiere tocar ninguna vista.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // SEC-03: fuerza el cambio de password de cuentas marcadas (semilla u otras) antes de cualquier accion.
    options.Filters.Add<ForzarCambioPasswordFilter>();
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

// CFG-02: en Azure App Service el TLS se termina en el proxy de la plataforma; sin esto,
// Request.IsHttps es false detras del proxy, lo que rompe cookies Secure, UseHttpsRedirection y las
// URLs absolutas del callback de Google. KnownNetworks/KnownProxies se vacian porque App Service no
// expone la app detras de un proxy publico adicional identificable de antemano.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Registro/Login");
    app.UseHsts();
}

// SEC-05: cabeceras de seguridad basicas (defensa en profundidad; complementan SameSite=Lax y HttpOnly).
// DEUDA TECNICA (registrada 2026-07-04, no bloqueante): script-src usa 'unsafe-inline' porque
// _LayoutAltar.cshtml y varias vistas tienen <script> inline (tema, sidebar, datepicker). Esto reduce
// bastante el valor de la CSP contra XSS (un script inyectado inline no seria bloqueado). Para cerrarlo
// habria que mover esos scripts a archivos estaticos o usar nonces por request; queda pendiente, no se
// resolvio en esta sesion.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com; " +
        "font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self';";
    await next();
});

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
