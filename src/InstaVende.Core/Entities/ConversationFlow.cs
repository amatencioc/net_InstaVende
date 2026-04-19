namespace InstaVende.Core.Entities;
public class ConversationFlow
{
    public int Id { get; set; }
    public int BotConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StepsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public bool IsEntryFlow { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public BotConfig BotConfig { get; set; } = null!;
}
