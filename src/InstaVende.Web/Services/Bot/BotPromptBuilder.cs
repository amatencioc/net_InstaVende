using System.Text.Json;
using System.Text.RegularExpressions;
using OpenAI.Chat;

namespace InstaVende.Web.Services.Bot;

/// <summary>Parámetros del modelo leídos de BotPrompts/MasterBot_LLMConfig.json.</summary>
public sealed record LlmConfig(float Temperature = 0.4f, int MaxTokens = 500, float TopP = 0.9f,
    float FrequencyPenalty = 0.3f, float PresencePenalty = 0.1f);

/// <summary>
/// Singleton que carga las plantillas del prompt maestro, las herramientas y la
/// configuración del modelo desde BotPrompts/, y resuelve los placeholders.
/// El system prompt (estable por negocio) va primero y el contexto dinámico al final
/// para aprovechar la caché de prefijos de OpenAI.
/// </summary>
public sealed partial class BotPromptBuilder
{
    private readonly ILogger<BotPromptBuilder> _logger;
    private readonly Lazy<(string System, string Dynamic)> _templates;
    private readonly Lazy<IReadOnlyList<ChatTool>> _tools;
    private readonly Lazy<LlmConfig> _llmConfig;

    // {{KEY}} con espacios opcionales. Los valores insertados nunca se re-expanden (una sola pasada).
    [GeneratedRegex(@"\{\{\s*([A-Z0-9_]+)\s*\}\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex PlaceholderRegex();

    public BotPromptBuilder(IWebHostEnvironment env, ILogger<BotPromptBuilder> logger)
    {
        _logger = logger;
        var basePath = Path.Combine(env.ContentRootPath, "BotPrompts");
        _templates = new(() => LoadTemplates(basePath));
        _tools     = new(() => LoadTools(basePath));
        _llmConfig = new(() => LoadLlmConfig(basePath));
    }

    public IReadOnlyList<ChatTool> Tools => _tools.Value;
    public LlmConfig LlmConfig => _llmConfig.Value;

    public string BuildSystemPrompt(IReadOnlyDictionary<string, string> staticValues, IReadOnlyDictionary<string, string> dynamicValues)
    {
        var (system, dynamic) = _templates.Value;
        return Resolve(system, staticValues) + "\n\n" + Resolve(dynamic, dynamicValues);
    }

    /// <summary>Registra en el log cualquier desajuste entre MasterBot_Functions.json y las implementaciones.</summary>
    public void ValidateTools(IReadOnlyCollection<string> implemented)
    {
        var declared = Tools.Select(t => t.FunctionName).ToHashSet(StringComparer.Ordinal);
        foreach (var missing in declared.Except(implemented))
            _logger.LogError("La función '{Tool}' está declarada en MasterBot_Functions.json pero no tiene implementación.", missing);
        foreach (var extra in implemented.Except(declared))
            _logger.LogWarning("La función '{Tool}' está implementada pero no declarada en MasterBot_Functions.json.", extra);
    }

    private string Resolve(string template, IReadOnlyDictionary<string, string> values)
        => PlaceholderRegex().Replace(template, m =>
        {
            if (values.TryGetValue(m.Groups[1].Value, out var v)) return v;
            _logger.LogWarning("Placeholder sin valor en el prompt: {Key}", m.Groups[1].Value);
            return string.Empty;
        });

    private (string, string) LoadTemplates(string basePath)
    {
        var systemPath  = Path.Combine(basePath, "MasterBot_SystemPrompt.md");
        var contextPath = Path.Combine(basePath, "MasterBot_DynamicContext.md");
        if (!File.Exists(systemPath) || !File.Exists(contextPath))
            throw new FileNotFoundException($"No se encontraron las plantillas del prompt en '{basePath}'.");

        _logger.LogInformation("Plantillas del prompt maestro cargadas desde {Path}", basePath);
        return (File.ReadAllText(systemPath, System.Text.Encoding.UTF8), File.ReadAllText(contextPath, System.Text.Encoding.UTF8));
    }

    private IReadOnlyList<ChatTool> LoadTools(string basePath)
    {
        var path = Path.Combine(basePath, "MasterBot_Functions.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning("No existe {Path}; el bot funcionará sin herramientas.", path);
            return [];
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
        var tools = doc.RootElement.GetProperty("functions").EnumerateArray()
            .Select(fn => ChatTool.CreateFunctionTool(
                fn.GetProperty("name").GetString()!,
                fn.GetProperty("description").GetString()!,
                BinaryData.FromString(fn.GetProperty("parameters").GetRawText())))
            .ToList();
        _logger.LogInformation("Cargadas {Count} herramientas del bot", tools.Count);
        return tools;
    }

    private LlmConfig LoadLlmConfig(string basePath)
    {
        var path = Path.Combine(basePath, "MasterBot_LLMConfig.json");
        if (!File.Exists(path)) return new LlmConfig();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var r = doc.RootElement;
            float F(string k, float d) => r.TryGetProperty(k, out var v) && v.TryGetDouble(out var x) ? (float)x : d;
            int I(string k, int d) => r.TryGetProperty(k, out var v) && v.TryGetInt32(out var x) ? x : d;
            return new LlmConfig(F("temperature", 0.4f), I("max_tokens", 500), F("top_p", 0.9f),
                F("frequency_penalty", 0.3f), F("presence_penalty", 0.1f));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "MasterBot_LLMConfig.json inválido; se usan valores por defecto.");
            return new LlmConfig();
        }
    }
}
