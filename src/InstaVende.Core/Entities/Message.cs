using InstaVende.Core.Enums;
namespace InstaVende.Core.Entities;
public class Message
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public MessageDirection Direction { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public string? ExternalMessageId { get; set; }
    public bool IsRead { get; set; }
    public bool SentByBot { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public Conversation Conversation { get; set; } = null!;
}
