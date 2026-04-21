namespace InstaVende.Core.Entities;

public class ConversationLabel
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6366f1";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Business Business { get; set; } = null!;
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
