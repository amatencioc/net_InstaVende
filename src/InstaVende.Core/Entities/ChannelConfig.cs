using InstaVende.Core.Enums;
namespace InstaVende.Core.Entities;
public class ChannelConfig
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public ChannelType ChannelType { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? PageId { get; set; }
    public string? InstagramAccountId { get; set; }
    public string AccessTokenEncrypted { get; set; } = string.Empty;
    public string? AppSecretEncrypted { get; set; }
    public string? WebhookVerifyToken { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Business Business { get; set; } = null!;
}
