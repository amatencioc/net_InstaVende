namespace InstaVende.Core.Entities;
public class Product
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Business Business { get; set; } = null!;
    public ProductCategory? Category { get; set; }
}
