namespace InstaVende.Core.Entities;
public class BotIntent
{
    public int Id { get; set; }
    public int BotConfigId { get; set; }
    public string IntentName { get; set; } = string.Empty;
    public string TriggerPhrases { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public BotConfig BotConfig { get; set; } = null!;
}
