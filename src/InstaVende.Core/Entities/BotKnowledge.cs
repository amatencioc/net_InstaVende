namespace InstaVende.Core.Entities;
public class BotKnowledge
{
    public int Id { get; set; }
    public int BotConfigId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public BotConfig BotConfig { get; set; } = null!;
}
