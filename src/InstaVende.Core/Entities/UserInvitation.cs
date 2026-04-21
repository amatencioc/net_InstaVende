using InstaVende.Core.Enums;

namespace InstaVende.Core.Entities;

public class UserInvitation
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    public Business Business { get; set; } = null!;
}
