namespace InstaVende.Core.Entities;

public class NotificationEmail
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Business Business { get; set; } = null!;
}
