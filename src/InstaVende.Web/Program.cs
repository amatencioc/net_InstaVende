using InstaVende.Core.Entities;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Repositories;
using InstaVende.Infrastructure.Services;
using InstaVende.Web.Hubs;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/instavende-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequireDigit = true;
    o.Password.RequiredLength = 6;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase = false;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddErrorDescriber<SpanishIdentityErrorDescriber>();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.LogoutPath = "/Account/Logout";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;
});

builder.Services.AddDataProtection();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();
builder.Services.AddHttpClients(builder.Configuration);

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<DataProtectionService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ImageService>();
builder.Services.AddSingleton<MasterPromptBuilder>();
builder.Services.AddValidatedOptions(builder.Configuration);
builder.Services.AddScoped<IBotEngineService, BotEngineService>();
builder.Services.AddScoped<IChannelMessageSender, WhatsAppService>();
builder.Services.AddScoped<IChannelMessageSender, MetaMessengerService>();
builder.Services.AddScoped<IChannelMessageSender, InstagramService>();

builder.Services.AddControllersWithViews(o =>
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<InstaVende.Web.Services.WaClientHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<InstaVende.Web.Services.WaClientHostedService>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // MigrateAsync ya consulta __EFMigrationsHistory internamente;
        // GetPendingMigrationsAsync() es redundante y añade un roundtrip extra.
        await db.Database.MigrateAsync();

        var rm = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        // Carga todos los roles en 1 query en lugar de N × RoleExistsAsync
        var requiredRoles  = new[] { "Admin", "Merchant", "Member" };
        var existingRoles  = rm.Roles.Select(r => r.Name).ToHashSet();
        foreach (var role in requiredRoles.Where(r => !existingRoles.Contains(r)))
            await rm.CreateAsync(new IdentityRole(role));
    }
    catch (Exception ex)
    {
        var startupLog = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        startupLog.LogError(ex, "Startup seeding failed  the application may not function correctly");
    }
}

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapHub<InboxHub>("/inboxHub");
app.Run();
