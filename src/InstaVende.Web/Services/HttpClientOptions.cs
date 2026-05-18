using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services;

/// <summary>
/// Opciones tipadas para una entrada individual dentro de la sección
/// <c>HttpClients:{name}</c> de <c>appsettings.json</c>.
/// </summary>
public sealed class HttpClientOptions
{
    /// <summary>
    /// Prefijo de sección padre. Cada cliente usa <c>HttpClients:{name}</c>.
    /// </summary>
    public const string SectionName = "HttpClients";

    /// <summary>
    /// Tiempo máximo de espera en segundos para este cliente HTTP.
    /// Debe ser un valor positivo mayor que cero.
    /// </summary>
    public int TimeoutSeconds { get; set; }
}

/// <summary>
/// Valida <see cref="HttpClientOptions"/> en el arranque de la aplicación.
/// Se aplica a todos los entornos sin excepción: un timeout de cero o negativo
/// es siempre un error de configuración, independientemente del entorno.
/// </summary>
internal sealed class HttpClientOptionsValidator : IValidateOptions<HttpClientOptions>
{
    // El nombre del cliente se inyecta para personalizar el mensaje de error.
    private readonly string _clientName;

    public HttpClientOptionsValidator(string clientName) => _clientName = clientName;

    public ValidateOptionsResult Validate(string? name, HttpClientOptions options)
    {
        if (options.TimeoutSeconds <= 0)
            return ValidateOptionsResult.Fail(
                $"HttpClients:{_clientName}:TimeoutSeconds debe ser mayor que 0. " +
                $"Valor actual: {options.TimeoutSeconds}. " +
                $"Corrígelo en appsettings.json o en la variable de entorno " +
                $"HttpClients__{_clientName}__TimeoutSeconds.");

        return ValidateOptionsResult.Success;
    }
}
