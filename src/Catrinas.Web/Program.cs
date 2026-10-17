using Catrinas.Web;
using Catrinas.Web.Datos;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

// Catrinas.Web: plataforma independiente del Concurso de Catrinas. Solo usa su propia base
// (ConnectionStrings:CatrinasContext) y la infraestructura transversal de Concurso.Core.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CatrinasDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CatrinasContext")));

// Cookie de sesion y llaves de Data Protection con nombre propio de Catrinas (configurables), para
// que una sesion de Altares no pueda confundirse con una de Catrinas aunque compartan dominio padre.
builder.Services.AddConcursoSesion(builder.Configuration, nombreCookiePorDefecto: ".Catrinas.Sesion");
builder.Services.AddConcursoDataProtection(builder.Configuration, applicationNamePorDefecto: "Catrinas.Web");

builder.Services.AddAuthentication(options => options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => options.Cookie.Name = ".Catrinas.Externa")
    .AddGoogleInstitucional(builder.Configuration);

builder.Services.AddConcursoLimitesLogin(builder.Configuration);
builder.Services.AddScoped<IVerificadorCambioPassword, VerificadorCambioPasswordJuez>();
builder.Services.AddSingleton(sp => new ServicioCorreoSmtp(sp.GetRequiredService<IConfiguration>(), "Concurso de Catrinas FIM"));
builder.Services.AddScoped<ConstanciaCatrinaService>();
builder.Services.AddAntiforgery(options => options.Cookie.Name = ".Catrinas.Antiforgery");

builder.Services.AddControllersWithViews(options => options.AgregarFiltrosSeguridad());

var app = builder.Build();

try
{
    SemillaCatrinas.Inicializar(app.Services);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "No se pudo inicializar la base de datos de Catrinas.");
    throw;
}

app.UseConcursoSeguridad("/Registro/Login");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Registro}/{action=Login}/{id?}");

app.Run();

// Expone la clase generada por las top-level statements para WebApplicationFactory en tests/Catrinas.Tests.
public partial class Program { }
