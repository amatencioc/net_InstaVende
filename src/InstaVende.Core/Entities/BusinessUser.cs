using InstaVende.Core.Enums;

namespace InstaVende.Core.Entities;

public class BusinessUser
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Business Business { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
