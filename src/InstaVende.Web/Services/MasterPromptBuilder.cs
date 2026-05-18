using System.Text.Json;
using System.Text.RegularExpressions;
using OpenAI.Chat;

namespace InstaVende.Web.Services;

/// <summary>
/// Singleton service that loads and resolves the master bot prompt templates.
/// Templates are cached in memory after the first load.
/// </summary>
public class MasterPromptBuilder
{
    private readonly string _basePath;
    private readonly ILogger<MasterPromptBuilder> _logger;

    private string? _systemPromptTemplate;
    private string? _dynamicContextTemplate;
    private IReadOnlyList<ChatTool>? _cachedTools;
    private LlmConfig? _cachedLlmConfig;
    private readonly object _lock = new();

    // Compilados una sola vez a nivel de tipo — evitan el parse del patrón en cada llamada
    private static readonly Regex _defaultValueRegex = new(
        @"\{\{[^|}]+\|\s*""([^""]*)""\s*\}\}",
        RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private static readonly Regex _unresolvedRegex = new(
        @"\{\{[^}]+\}\}",
        RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public MasterPromptBuilder(IWebHostEnvironment env, ILogger<MasterPromptBuilder> logger)
    {
        _basePath = Path.Combine(env.ContentRootPath, "BotPrompts");
        _logger = logger;
    }

    /// <summary>
    /// Builds the complete system prompt (master + dynamic context) with all
    /// {{placeholders}} replaced by the provided values.
    /// </summary>
    public string BuildSystemPrompt(Dictionary<string, string> placeholders)
    {
        EnsureTemplatesLoaded();
        var combined = _systemPromptTemplate! + "\n\n" + _dynamicContextTemplate!;
        return ResolvePlaceholders(combined, placeholders);
    }

    /// <summary>
    /// Loads and caches the list of ChatTools from MasterBot_Functions.json.
    /// Compatible with OpenAI Function Calling (tools parameter).
    /// </summary>
    public IReadOnlyList<ChatTool> LoadTools()
    {
        if (_cachedTools != null) return _cachedTools;

        lock (_lock)
        {
            if (_cachedTools != null) return _cachedTools;

            var functionsPath = Path.Combine(_basePath, "MasterBot_Functions.json");
            if (!File.Exists(functionsPath))
            {
                _logger.LogWarning("MasterBot_Functions.json not found at {Path}", functionsPath);
                return _cachedTools = Array.Empty<ChatTool>();
            }

            var json = File.ReadAllText(functionsPath);
            using var doc = JsonDocument.Parse(json);
            var tools = new List<ChatTool>();

            foreach (var fn in doc.RootElement.GetProperty("functions").EnumerateArray())
            {
                var name = fn.GetProperty("name").GetString()!;
                var description = fn.GetProperty("description").GetString()!;
                var parameters = fn.GetProperty("parameters").GetRawText();
                tools.Add(ChatTool.CreateFunctionTool(name, description, BinaryData.FromString(parameters)));
            }

            _cachedTools = tools.AsReadOnly();
            _logger.LogInformation("Loaded {Count} bot tools from MasterBot_Functions.json", tools.Count);
        }

        return _cachedTools;
    }

    // ?? Private helpers ????????????????????????????????????????????????????????

    private void EnsureTemplatesLoaded()
    {
        if (_systemPromptTemplate != null) return;

        lock (_lock)
        {
            if (_systemPromptTemplate != null) return;

            var systemPath  = Path.Combine(_basePath, "MasterBot_SystemPrompt.md");
            var contextPath = Path.Combine(_basePath, "MasterBot_DynamicContext.md");

            if (!File.Exists(systemPath) || !File.Exists(contextPath))
                throw new FileNotFoundException(
                    $"Bot prompt files not found in '{_basePath}'. " +
                    "Ensure BotPrompts/ folder is present in the web project.");

            _systemPromptTemplate  = File.ReadAllText(systemPath);
            _dynamicContextTemplate = File.ReadAllText(contextPath);
            _logger.LogInformation("Master bot prompt templates loaded from {Path}", _basePath);
        }
    }

    /// <summary>
    /// Replaces {{KEY}} with the corresponding value.
    /// Handles {{KEY | "default"}} syntax for optional placeholders.
    /// Removes any remaining unresolved placeholders.
    /// </summary>
    private static string ResolvePlaceholders(string template, Dictionary<string, string> values)
    {
        // 1. Replace known keys
        foreach (var kv in values)
            template = template.Replace($"{{{{{kv.Key}}}}}", kv.Value);

        // 2. Replace {{KEY | "default"}} patterns — extract the default value
        template = _defaultValueRegex.Replace(template, "$1");

        // 3. Remove any remaining unresolved placeholders
        template = _unresolvedRegex.Replace(template, string.Empty);

        return template;
    }

    /// <summary>
    /// Loads LLM configuration from MasterBot_LLMConfig.json.
    /// Returns safe defaults if the file is missing or malformed.
    /// </summary>
    public LlmConfig LoadLlmConfig()
    {
        if (_cachedLlmConfig != null) return _cachedLlmConfig;

        lock (_lock)
        {
            if (_cachedLlmConfig != null) return _cachedLlmConfig;

            var path = Path.Combine(_basePath, "MasterBot_LLMConfig.json");
            if (!File.Exists(path))
            {
                _logger.LogWarning("MasterBot_LLMConfig.json not found, using defaults.");
                return _cachedLlmConfig = new LlmConfig();
            }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var r = doc.RootElement;
                return _cachedLlmConfig = new LlmConfig
                {
                    Temperature      = r.TryGetProperty("temperature",       out var t)  ? (float)t.GetDouble()  : 0.4f,
                    MaxTokens        = r.TryGetProperty("max_tokens",        out var mt) ? mt.GetInt32()          : 500,
                    TopP             = r.TryGetProperty("top_p",             out var tp) ? (float)tp.GetDouble() : 0.9f,
                    FrequencyPenalty = r.TryGetProperty("frequency_penalty", out var fp) ? (float)fp.GetDouble() : 0.3f,
                    PresencePenalty  = r.TryGetProperty("presence_penalty",  out var pp) ? (float)pp.GetDouble() : 0.1f,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse MasterBot_LLMConfig.json, using defaults.");
                return _cachedLlmConfig = new LlmConfig();
            }
        }
    }
}

/// <summary>Typed representation of MasterBot_LLMConfig.json.</summary>
public sealed class LlmConfig
{
    public float Temperature      { get; init; } = 0.4f;
    public int   MaxTokens        { get; init; } = 500;
    public float TopP             { get; init; } = 0.9f;
    public float FrequencyPenalty { get; init; } = 0.3f;
    public float PresencePenalty  { get; init; } = 0.1f;
}
