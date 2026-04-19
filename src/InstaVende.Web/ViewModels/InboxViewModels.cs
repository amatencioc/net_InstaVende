using InstaVende.Core.Enums;
namespace InstaVende.Web.ViewModels;

public class ConversationListItemViewModel
{
    public int Id { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? AvatarUrl { get; set; }
    public ChannelType Channel { get; set; }
    public ConversationStatus Status { get; set; }
    public string? LastMessage { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UnreadCount { get; set; }
}

public class MessageViewModel
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public MessageDirection Direction { get; set; }
    public bool SentByBot { get; set; }
    public DateTime SentAt { get; set; }
}

public class SendMessageViewModel
{
    public int ConversationId { get; set; }
    public string Message { get; set; } = string.Empty;
}
