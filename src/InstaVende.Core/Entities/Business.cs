namespace InstaVende.Core.Entities;
public class Business
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? FacebookPageId { get; set; }
    public string? InstagramAccountId { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? BusinessSector { get; set; }
    public string? SocialMedia { get; set; }
    public string? Sector { get; set; }
    public int CompletionPercent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Country { get; set; }
    public string? Currency { get; set; }
    public bool IsActive { get; set; } = true;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public BotConfig? BotConfig { get; set; }
    public VendorConfig? VendorConfig { get; set; }
    public ICollection<BusinessUser> BusinessUsers { get; set; } = new List<BusinessUser>();
    public ICollection<UserInvitation> UserInvitations { get; set; } = new List<UserInvitation>();
    public ICollection<NotificationEmail> NotificationEmails { get; set; } = new List<NotificationEmail>();
    public ICollection<ReminderTemplate> ReminderTemplates { get; set; } = new List<ReminderTemplate>();
    public ICollection<KnowledgeEntry> KnowledgeEntries { get; set; } = new List<KnowledgeEntry>();
    public ICollection<DeliveryZone> DeliveryZones { get; set; } = new List<DeliveryZone>();
    public ICollection<PaymentImage> PaymentImages { get; set; } = new List<PaymentImage>();
    public ICollection<ConversationLabel> ConversationLabels { get; set; } = new List<ConversationLabel>();
    public ICollection<ChannelConfig> ChannelConfigs { get; set; } = new List<ChannelConfig>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<PaymentMethod> PaymentMethods { get; set; } = new List<PaymentMethod>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
    public OnboardingProgress? OnboardingProgress { get; set; }
}
