using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
namespace InstaVende.Web.ViewModels;

public class ProductViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
    [StringLength(300, ErrorMessage = "El nombre no puede superar los 300 caracteres.")]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Sku { get; set; }
    [Required(ErrorMessage = "El precio es obligatorio.")]
    [Range(0, 9999999, ErrorMessage = "El precio debe estar entre 0 y 9.999.999.")]
    public decimal Price { get; set; }
    public decimal? PriceOriginal { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
    public int Stock { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ProductVariantViewModel> Variants { get; set; } = new();
}

public class ProductListViewModel
{
    public IEnumerable<ProductViewModel> Products { get; set; } = Enumerable.Empty<ProductViewModel>();
    public IEnumerable<CategoryViewModel> Categories { get; set; } = Enumerable.Empty<CategoryViewModel>();
    public string? SearchTerm { get; set; }
    public int? SelectedCategoryId { get; set; }
}

public class CategoryViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre de la categoría es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ProductVariantViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    [Required(ErrorMessage = "El atributo es obligatorio.")]
    public string AttributeName { get; set; } = string.Empty;
    [Required(ErrorMessage = "El valor es obligatorio.")]
    public string AttributeValue { get; set; } = string.Empty;
    public decimal PriceModifier { get; set; }
    public int Stock { get; set; }
    public string? Sku { get; set; }
    public bool IsActive { get; set; } = true;
}
