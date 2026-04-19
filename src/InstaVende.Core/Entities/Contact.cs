using InstaVende.Core.Enums;
namespace InstaVende.Core.Entities;
public class Contact
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public ChannelType ChannelType { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public Business Business { get; set; } = null!;
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
