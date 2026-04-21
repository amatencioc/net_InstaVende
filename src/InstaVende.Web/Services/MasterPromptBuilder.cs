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
    private readonly object _lock = new();

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
        template = Regex.Replace(
            template,
            @"\{\{[^|}]+\|\s*""([^""]*)""\s*\}\}",
            "$1");

        // 3. Remove any remaining unresolved placeholders
        template = Regex.Replace(template, @"\{\{[^}]+\}\}", string.Empty);

        return template;
    }
}
