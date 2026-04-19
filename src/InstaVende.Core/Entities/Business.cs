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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public BotConfig? BotConfig { get; set; }
    public ICollection<ChannelConfig> ChannelConfigs { get; set; } = new List<ChannelConfig>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
