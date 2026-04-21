namespace InstaVende.Core.Entities;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string AttributeName { get; set; } = string.Empty;
    public string AttributeValue { get; set; } = string.Empty;
    public decimal PriceModifier { get; set; } = 0;
    public int Stock { get; set; }
    public string? Sku { get; set; }
    public bool IsActive { get; set; } = true;
    public Product Product { get; set; } = null!;
}
