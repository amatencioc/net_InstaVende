using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services.Channels;

/// <summary>Sección "WhatsAppClient": proceso Node (wa-client) que gestiona las sesiones QR por negocio.</summary>
public sealed class WhatsAppClientOptions
{
    public const string SectionName = "WhatsAppClient";

    /// <summary>URL base del servidor Node, p.ej. "http://127.0.0.1:3001".</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:3001";

    /// <summary>Ruta al directorio wa-client (absoluta o relativa a la raíz de contenido del proyecto web).</summary>
    public string ClientPath { get; set; } = "../../wa-client";

    /// <summary>Si es true la app lanza y supervisa el proceso Node.</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Secreto compartido entre .NET y Node (cabecera X-WA-Key y firma X-WA-Signature).
    /// Si se deja vacío y AutoStart=true se genera uno aleatorio por proceso.
    /// </summary>
    public string SharedSecret { get; set; } = string.Empty;

    /// <summary>
    /// URL a la que Node envía los mensajes entrantes. Si está vacía se usa la dirección HTTP
    /// en la que escucha esta aplicación.
    /// </summary>
    public string? CallbackBaseUrl { get; set; }

    /// <summary>Directorio donde Node guarda las sesiones por negocio. Vacío = wa-client/session.</summary>
    public string? SessionDir { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 15;
    public int HealthCheckIntervalSeconds { get; set; } = 15;

    /// <summary>Health checks fallidos consecutivos antes de reiniciar el proceso Node.</summary>
    public int UnhealthyThreshold { get; set; } = 3;

    public string ResolveClientPath(string contentRootPath)
        => Path.GetFullPath(Path.IsPathRooted(ClientPath) ? ClientPath : Path.Combine(contentRootPath, ClientPath));
}

/// <summary>Genera el secreto compartido cuando no se configuró y la app lanza Node por sí misma.</summary>
internal sealed class WhatsAppClientOptionsSetup : IPostConfigureOptions<WhatsAppClientOptions>
{
    private static readonly string GeneratedSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public void PostConfigure(string? name, WhatsAppClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SharedSecret) && options.AutoStart)
            options.SharedSecret = GeneratedSecret;
        options.BaseUrl = options.BaseUrl.TrimEnd('/');
    }
}

internal sealed class WhatsAppClientOptionsValidator : IValidateOptions<WhatsAppClientOptions>
{
    public ValidateOptionsResult Validate(string? name, WhatsAppClientOptions o)
    {
        if (!Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return ValidateOptionsResult.Fail($"WhatsAppClient:BaseUrl '{o.BaseUrl}' no es una URL HTTP/HTTPS válida.");
        if (string.IsNullOrWhiteSpace(o.SharedSecret))
            return ValidateOptionsResult.Fail("WhatsAppClient:SharedSecret es obligatorio cuando AutoStart=false (Node se gestiona externamente).");
        if (o.RequestTimeoutSeconds <= 0 || o.HealthCheckIntervalSeconds <= 0 || o.UnhealthyThreshold <= 0)
            return ValidateOptionsResult.Fail("WhatsAppClient: los tiempos y umbrales deben ser mayores que 0.");
        return ValidateOptionsResult.Success;
    }
}
