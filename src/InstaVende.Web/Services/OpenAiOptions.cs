using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services;

/// <summary>
/// Strongly-typed binding for the "OpenAI" section in appsettings.json.
/// </summary>
public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>Secret key obtained from https://platform.openai.com/api-keys</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model identifier, e.g. "gpt-4o" or "gpt-4o-mini".</summary>
    public string Model { get; set; } = "gpt-4o";
}

/// <summary>
/// Validates <see cref="OpenAiOptions"/> at application startup.
/// Only enforces <see cref="OpenAiOptions.ApiKey"/> in non-Development environments
/// so local runs without a key still work.
/// </summary>
internal sealed class OpenAiOptionsValidator : IValidateOptions<OpenAiOptions>
{
    private readonly IHostEnvironment _env;

    public OpenAiOptionsValidator(IHostEnvironment env) => _env = env;

    public ValidateOptionsResult Validate(string? name, OpenAiOptions options)
    {
        // Allow empty key in Development so the app boots without a real key
        if (_env.IsDevelopment())
            return ValidateOptionsResult.Success;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return ValidateOptionsResult.Fail(
                "OpenAI:ApiKey no está configurado. " +
                "Establece el valor en appsettings.Production.json o en la " +
                "variable de entorno OpenAI__ApiKey antes de iniciar la aplicación.");

        if (string.IsNullOrWhiteSpace(options.Model))
            return ValidateOptionsResult.Fail(
                "OpenAI:Model no puede estar vacío. " +
                "Valor recomendado: \"gpt-4o\".");

        return ValidateOptionsResult.Success;
    }
}
