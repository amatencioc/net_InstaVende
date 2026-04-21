namespace InstaVende.Core.Entities;

public class OnboardingProgress
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public bool ChannelConnected { get; set; }
    public bool ProductsAdded { get; set; }
    public bool PaymentMethodsAdded { get; set; }
    public bool BotConfigured { get; set; }
    public bool BotTested { get; set; }
    public bool OnboardingCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Business Business { get; set; } = null!;
}
