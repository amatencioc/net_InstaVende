namespace InstaVende.Core.Interfaces;
public interface IBotEngineService
{
    Task<string> ProcessMessageAsync(int businessId, int conversationId, string incomingMessage);

    /// <summary>Evicts the cached BotConfig so the next message reads fresh data from the DB.</summary>
    void InvalidateBotConfigCache(int businessId);

    /// <summary>Evicts the cached KnowledgeEntries so the next AI call reads fresh data from the DB.</summary>
    void InvalidateKnowledgeCache(int businessId);
}
