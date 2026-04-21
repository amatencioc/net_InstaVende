namespace InstaVende.Core.Entities;

public class VendorConfig
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string? VendorGender { get; set; }
    public string? Country { get; set; }
    public string? BusinessDescription { get; set; }
    public string? TargetAudience { get; set; }
    public string? Rules { get; set; }
    public string? CommunicationStyle { get; set; }
    public string? SalesStyle { get; set; }
    public int ResponseLength { get; set; } = 3;
    public bool UseEmojis { get; set; } = true;
    public bool UseOpeningPunctuation { get; set; } = false;
    public string? WordsToAvoid { get; set; }
    public string? EmojiPalette { get; set; }
    public string? WelcomeMessage { get; set; }
    public string? WelcomeMediaUrl { get; set; }
    public string? PurchaseConfirmationMessage { get; set; }
    public string? HumanHandoffSituations { get; set; }
    public bool AutoPauseOnHandoff { get; set; } = true;
    public string? HandoffExampleMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Business Business { get; set; } = null!;
}
