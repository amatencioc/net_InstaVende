namespace InstaVende.Web.Services;

/// <summary>
/// Configurable behaviour settings for <see cref="BotEngineService"/>.
/// Bound from the "BotCache" section in appsettings.json.
/// </summary>
public sealed class BotCacheOptions
{
    public const string SectionName = "BotCache";

    /// <summary>Minutes to cache the active BotConfig (intents, knowledge base, vendor config).</summary>
    public int BotConfigTtlMinutes { get; set; } = 5;

    /// <summary>Minutes to cache the KnowledgeEntries used in AI prompts.</summary>
    public int KnowledgeEntriesTtlMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum number of past messages loaded per conversation to build the AI context window.
    /// Higher values give the model more context but increase token usage and latency.
    /// </summary>
    public int MaxHistoryMessages { get; set; } = 10;

    /// <summary>
    /// Maximum number of tool-call iterations per AI turn before forcing a final answer.
    /// Prevents infinite loops when the model keeps calling tools without converging.
    /// </summary>
    public int MaxToolIterations { get; set; } = 5;
}
