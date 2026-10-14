namespace InstaVende.Core.Interfaces;

/// <summary>Respuesta del vendedor IA. <see cref="Text"/> es null cuando el bot no debe responder.</summary>
public sealed record BotReply(string? Text, bool HandoffTriggered = false)
{
    public static readonly BotReply Silent = new((string?)null);
}

public interface IBotEngineService
{
    /// <summary>
    /// Genera la respuesta del bot para el último mensaje entrante de la conversación.
    /// El mensaje ya debe estar persistido; <paramref name="conversationId"/> &lt;= 0 indica vista previa.
    /// </summary>
    Task<BotReply> ProcessMessageAsync(int businessId, int conversationId, string incomingMessage, CancellationToken ct = default);
}
