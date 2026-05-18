using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services;

/// <summary>
/// Strongly-typed binding for the "WhatsAppClient" section in appsettings.json.
/// </summary>
public sealed class WhatsAppClientOptions
{
    public const string SectionName = "WhatsAppClient";

    /// <summary>URL base del servidor Node local, p.ej. "http://localhost:3001".</summary>
    public string BaseUrl { get; set; } = "http://localhost:3001";

    /// <summary>Ruta absoluta o relativa al directorio wa-client que contiene index.js.</summary>
    public string ClientPath { get; set; } = "../wa-client";

    /// <summary>Si es <c>true</c>, el hosted service arrancará el proceso Node al iniciar la app.</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Número de polls consecutivos fallidos de <c>WaStatus</c> antes de disparar
    /// un auto-restart del proceso Node. Mínimo 1. Por defecto 3.
    /// Configurable en <c>appsettings.json</c> bajo <c>WhatsAppClient:OfflineFailThreshold</c>.
    /// </summary>
    public int OfflineFailThreshold { get; set; } = 3;
}

/// <summary>
/// Valida <see cref="WhatsAppClientOptions"/> en el arranque de la aplicación.
/// Solo exige <see cref="WhatsAppClientOptions.BaseUrl"/> en entornos que no sean Development,
/// para no bloquear la ejecución local cuando no hay un cliente WhatsApp activo.
/// </summary>
internal sealed class WhatsAppClientOptionsValidator : IValidateOptions<WhatsAppClientOptions>
{
    private readonly IHostEnvironment _env;

    public WhatsAppClientOptionsValidator(IHostEnvironment env) => _env = env;

    public ValidateOptionsResult Validate(string? name, WhatsAppClientOptions options)
    {
        // En desarrollo permitimos valores por defecto sin validación estricta
        if (_env.IsDevelopment())
            return ValidateOptionsResult.Success;

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            return ValidateOptionsResult.Fail(
                "WhatsAppClient:BaseUrl no está configurado. " +
                "Establece el valor en appsettings.Production.json o en la " +
                "variable de entorno WhatsAppClient__BaseUrl antes de iniciar la aplicación.");

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return ValidateOptionsResult.Fail(
                $"WhatsAppClient:BaseUrl '{options.BaseUrl}' no es una URL HTTP/HTTPS válida.");

        if (string.IsNullOrWhiteSpace(options.ClientPath))
            return ValidateOptionsResult.Fail(
                "WhatsAppClient:ClientPath no puede estar vacío. " +
                "Indica la ruta absoluta o relativa al directorio wa-client.");

        return ValidateOptionsResult.Success;
    }
}
