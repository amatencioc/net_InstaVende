namespace InstaVende.Core.Entities;

public class PaymentImage
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Business Business { get; set; } = null!;
}
