using InstaVende.Core.Enums;
namespace InstaVende.Core.Entities;
public class BotConfig
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string BotName { get; set; } = "Asistente";
    public string? Personality { get; set; }
    public string? BaseSystemPrompt { get; set; }
    public InteractionLevel InteractionLevel { get; set; } = InteractionLevel.Standard;
    public string Language { get; set; } = "es";
    public string FallbackMessage { get; set; } = "Lo siento, no entendí tu mensaje. ¿Puedes reformularlo?";
    public bool EnableHandoff { get; set; } = true;
    public string? HandoffTriggerPhrase { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Business Business { get; set; } = null!;
    public ICollection<BotIntent> Intents { get; set; } = new List<BotIntent>();
    public ICollection<BotKnowledge> KnowledgeBase { get; set; } = new List<BotKnowledge>();
    public ICollection<ConversationFlow> Flows { get; set; } = new List<ConversationFlow>();
}
