using System.Threading.RateLimiting;

namespace InstaVende.Web.Infrastructure;

public static class RateLimitPolicies
{
    /// <summary>Login/registro: 10 intentos por minuto por IP.</summary>
    public const string Auth = "auth";
    /// <summary>Webhooks de proveedores: 600 por minuto por IP.</summary>
    public const string Webhooks = "webhooks";
    /// <summary>Llamadas que consumen el modelo de IA desde el panel (vista previa del bot): 20 por minuto por usuario.</summary>
    public const string Llm = "llm";

    public static IServiceCollection AddInstaVendeRateLimiting(this IServiceCollection services)
        => services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
            o.AddPolicy(Webhooks, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1) }));
            o.AddPolicy(Llm, ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.Identity?.Name ?? ClientIp(ctx),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
        });

    private static string ClientIp(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
