using InstaVende.Core.Enums;
namespace InstaVende.Core.Entities;

public class PaymentMethod
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public PaymentMethodType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string? AccountAlias { get; set; }
    public string? AccountNumber { get; set; }
    public string? PaymentLink { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Business Business { get; set; } = null!;
}
