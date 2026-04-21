using InstaVende.Core.Enums;

namespace InstaVende.Core.Entities;

public class ReminderTemplate
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public CustomerSegment Segment { get; set; }
    public int Order { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TimeWindow { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? MediaUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Business Business { get; set; } = null!;
}
