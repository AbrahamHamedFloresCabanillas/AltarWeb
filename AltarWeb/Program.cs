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
        options.CallbackPath = "/Alumno/google-callback";
        options.Scope.Add("email");
        options.Scope.Add("profile");
    });

builder.Services.AddScoped<ConstanciaService>();
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
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
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
    pattern: "{controller=Acceso}/{action=Login}/{id?}");

app.Run();
