using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
namespace InstaVende.Web.ViewModels;

public class ProductViewModel
{
    public int Id { get; set; }
    [Required][StringLength(300)] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required][Range(0, 9999999)] public decimal Price { get; set; }
    [Range(0, int.MaxValue)] public int Stock { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; }
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
    [Required][StringLength(100)] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
