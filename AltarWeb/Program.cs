using System.Security.Cryptography;
using System.Text;
using AltarWeb.Models;
using AltarWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AltarDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AltarContext")
        ?? builder.Configuration.GetConnectionString("AltarWebContext")));

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
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
builder.Services.AddControllersWithViews();

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

    // Fixup idempotente: las contrasenas de Jueces creadas antes de esta version se
    // guardaban en texto plano. Las re-hashea en el arranque; no hace nada en las
    // ejecuciones siguientes porque ya quedan en formato hash (Base64 de 44 caracteres).
    try
    {
        var context = services.GetRequiredService<AltarDbContext>();
        var sinHashear = context.Jueces.IgnoreQueryFilters()
            .Where(j => j.Password.Length != 44)
            .ToList();
        foreach (var juez in sinHashear)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(juez.Password));
            juez.Password = Convert.ToBase64String(bytes);
        }
        if (sinHashear.Count > 0) context.SaveChanges();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrio un error al migrar contrasenas de Jueces a formato hash.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Registro/Login");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Registro}/{action=Login}/{id?}");

app.Run();