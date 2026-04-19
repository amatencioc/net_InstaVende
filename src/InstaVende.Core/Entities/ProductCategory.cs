namespace InstaVende.Core.Entities;
public class ProductCategory
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Business Business { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
