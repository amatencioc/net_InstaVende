using System.Text.Json.Serialization;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Services;
using InstaVende.Web.Infrastructure;
using InstaVende.Web.Services.Bot;
using InstaVende.Web.Services.Channels;
using InstaVende.Web.Services.Messaging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services;

/// <summary>Registro de servicios de la aplicación agrupado por responsabilidad.</summary>
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInstaVendeData(this IServiceCollection services, IConfiguration configuration, bool persistKeys = true)
    {
        services.AddMemoryCache();
        services.AddSingleton<BotContextInvalidationInterceptor>();
        services.AddDbContext<AppDbContext>((sp, o) => o
            .UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3))
            .AddInterceptors(sp.GetRequiredService<BotContextInvalidationInterceptor>()));

        // Claves en BD: los tokens cifrados sobreviven a reinicios y se comparten entre instancias.
        var dataProtection = services.AddDataProtection().SetApplicationName("InstaVende");
        if (persistKeys) dataProtection.PersistKeysToDbContext<AppDbContext>();
        services.AddScoped<DataProtectionService>();
        return services;
    }

    public static IServiceCollection AddInstaVendeIdentity(this IServiceCollection services, IHostEnvironment env)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>();

        services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = "iv.auth";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromDays(7);
            o.SlidingExpiration = true;
            // API: sin redirecciones HTML, solo códigos de estado
            o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        });

        services.AddAntiforgery(o =>
        {
            o.HeaderName = CsrfTokenIssuer.HeaderName;
            o.Cookie.Name = "iv.af";
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        services.AddSingleton<CsrfTokenIssuer>();
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUserService>();
        return services;
    }

    public static IServiceCollection AddInstaVendeApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()))
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            });
        services.AddProblemDetails();
        services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info.Title = "InstaVende API";
            doc.Info.Description = "API del panel de InstaVende (vendedor IA multicanal). Autenticación por cookie + cabecera X-XSRF-TOKEN.";
            return Task.CompletedTask;
        }));
        services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddInstaVendeRateLimiting();

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(o => o.AddDefaultPolicy(p =>
        {
            if (origins.Length > 0) p.WithOrigins(origins).AllowCredentials().AllowAnyHeader().AllowAnyMethod();
        }));
        return services;
    }

    /// <param name="validateOnStart">false al generar el documento OpenAPI en compilación (no hay secretos).</param>
    public static IServiceCollection AddInstaVendeOptions(this IServiceCollection services, IConfiguration configuration, bool validateOnStart = true)
    {
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<BotCacheOptions>(configuration.GetSection(BotCacheOptions.SectionName));

        services.AddOptions<OpenAiOptions>().Bind(configuration.GetSection(OpenAiOptions.SectionName));
        services.AddOptions<WhatsAppClientOptions>().Bind(configuration.GetSection(WhatsAppClientOptions.SectionName));
        services.AddSingleton<IPostConfigureOptions<WhatsAppClientOptions>, WhatsAppClientOptionsSetup>();
        if (!validateOnStart) return services;

        services.AddOptions<OpenAiOptions>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<OpenAiOptions>, OpenAiOptionsValidator>();
        services.AddOptions<WhatsAppClientOptions>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<WhatsAppClientOptions>, WhatsAppClientOptionsValidator>();
        return services;
    }

    public static IServiceCollection AddInstaVendeMessaging(this IServiceCollection services)
    {
        // Canales
        services.AddHttpClient<WhatsAppClientApi>();
        services.AddHttpClient<MetaGraphClient>();
        services.AddScoped<ChannelConfigProvider>();
        services.AddKeyedScoped<IChannelMessageSender, WhatsAppService>(ChannelType.WhatsApp);
        services.AddKeyedScoped<IChannelMessageSender, MetaMessengerService>(ChannelType.FacebookMessenger);
        services.AddKeyedScoped<IChannelMessageSender, InstagramService>(ChannelType.Instagram);
        services.AddSingleton<WaClientSupervisor>();
        services.AddHostedService(sp => sp.GetRequiredService<WaClientSupervisor>());

        // Pipeline de mensajes
        services.AddSingleton<InboundMessageQueue>();
        services.AddHostedService<InboundMessageProcessor>();
        services.AddScoped<IncomingMessageHandler>();
        services.AddScoped<MessageDispatcher>();
        services.AddScoped<InboxNotifier>();
        services.AddHostedService<ReminderDispatcher>();
        return services;
    }

    public static IServiceCollection AddInstaVendeBot(this IServiceCollection services)
    {
        services.AddSingleton<OpenAiChatClientAccessor>();
        services.AddSingleton<BotPromptBuilder>();
        services.AddScoped<BotContextProvider>();
        services.AddScoped<CartService>();
        services.AddScoped<BotToolExecutor>();
        services.AddScoped<IBotEngineService, BotEngineService>();
        services.AddScoped<ImageService>();
        return services;
    }
}
