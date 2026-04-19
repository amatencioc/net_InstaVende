namespace InstaVende.Core.Interfaces;
public interface IBotEngineService
{
    Task<string> ProcessMessageAsync(int businessId, int conversationId, string incomingMessage);
}
