using InstaVende.Infrastructure.Data;
using InstaVende.Web.Hubs;
using InstaVende.Web.Infrastructure;
using InstaVende.Web.Services;
using InstaVende.Web.Services.Bot;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Durante la generación del documento OpenAPI en compilación no hay BD ni servicios en segundo plano
var isGeneratingOpenApi = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services
    .AddInstaVendeOptions(builder.Configuration, validateOnStart: !isGeneratingOpenApi)
    .AddInstaVendeData(builder.Configuration, persistKeys: !isGeneratingOpenApi)
    .AddInstaVendeIdentity(builder.Environment)
    .AddInstaVendeApi(builder.Configuration)
    .AddInstaVendeBot();

if (!isGeneratingOpenApi)
    builder.Services.AddInstaVendeMessaging();

// Detrás de un proxy (nginx del SPA, balanceador): respeta X-Forwarded-Proto/For
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

if (!isGeneratingOpenApi)
    await InitializeDatabaseAsync(app);

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseSerilogRequestLogging();

// Imágenes subidas por los comercios (wwwroot/uploads)
app.UseStaticFiles();

// SPA opcional servido por la propia API (despliegue en un solo servidor)
var spaRoot = app.Configuration["Spa:RootPath"];
var serveSpa = !string.IsNullOrWhiteSpace(spaRoot) && Directory.Exists(Path.Combine(app.Environment.ContentRootPath, spaRoot));
if (serveSpa)
{
    var spaFiles = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, spaRoot!));
    app.UseStaticFiles(new StaticFileOptions { FileProvider = spaFiles });
}

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<InboxHub>("/hubs/inbox");
app.MapOpenApi();
if (app.Environment.IsDevelopment())
    app.MapScalarApiReference("/api/docs", o => o.WithTitle("InstaVende API"));

if (serveSpa)
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, spaRoot!))
    });

if (!isGeneratingOpenApi)
    app.Services.GetRequiredService<BotPromptBuilder>().ValidateTools(
        app.Services.CreateScope().ServiceProvider.GetRequiredService<BotToolExecutor>().ToolNames);

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var existing = roles.Roles.Select(r => r.Name).ToHashSet();
        foreach (var role in AppRoles.All.Where(r => !existing.Contains(r)))
            await roles.CreateAsync(new IdentityRole(role));
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "No se pudo inicializar la base de datos (migraciones/roles). La aplicación no arrancará.");
        throw;
    }
}

public partial class Program;
