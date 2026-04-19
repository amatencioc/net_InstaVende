using InstaVende.Core.Enums;
namespace InstaVende.Core.Entities;
public class Conversation
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int ContactId { get; set; }
    public ChannelType ChannelType { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.BotActive;
    public string? AssignedAgentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public Business Business { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
    public ApplicationUser? AssignedAgent { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
