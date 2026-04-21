using InstaVende.Core.Enums;
namespace InstaVende.Core.Entities;

public class Reminder
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int ContactId { get; set; }
    public int? ConversationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public ChannelType ChannelType { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;
    public DateTime ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? TemplateKey { get; set; }
    public string? CreatedByAgentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Business Business { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
    public ApplicationUser? CreatedByAgent { get; set; }
}
