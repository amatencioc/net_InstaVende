using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InstaVende.Web.Features.Products;

// ── Filtros y ordenación del listado ──────────────────────────────────────────

/// <summary>Filtro de estado del catálogo. Stock bajo = entre 1 y <see cref="ProductRules.LowStockThreshold"/> unidades.</summary>
public enum ProductStatusFilter
{
    [JsonStringEnumMemberName("active")] Active,
    [JsonStringEnumMemberName("inactive")] Inactive,
    [JsonStringEnumMemberName("lowStock")] LowStock,
    [JsonStringEnumMemberName("outOfStock")] OutOfStock,
}

/// <summary>Orden del listado de productos (por defecto, los más recientes primero).</summary>
public enum ProductSort
{
    [JsonStringEnumMemberName("newest")] Newest,
    [JsonStringEnumMemberName("oldest")] Oldest,
    [JsonStringEnumMemberName("nameAsc")] NameAsc,
    [JsonStringEnumMemberName("nameDesc")] NameDesc,
    [JsonStringEnumMemberName("priceAsc")] PriceAsc,
    [JsonStringEnumMemberName("priceDesc")] PriceDesc,
    [JsonStringEnumMemberName("stockAsc")] StockAsc,
    [JsonStringEnumMemberName("stockDesc")] StockDesc,
    // Orden manual del catálogo (SortOrder) usado también por el bot.
    [JsonStringEnumMemberName("manual")] Manual,
}

/// <summary>Reglas compartidas del módulo de productos.</summary>
public static class ProductRules
{
    public const int LowStockThreshold = 5;
    public const int MaxImages = 6;
    public const int MaxVariants = 100;
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const int SkuMaxLength = 64;
    public const double MaxPrice = 99_999_999.99;
}

// ── Lectura ───────────────────────────────────────────────────────────────────

/// <summary>Fila del listado de productos.</summary>
public sealed record ProductListItemDto(
    int Id,
    string Name,
    string? Sku,
    decimal Price,
    decimal? PriceOriginal,
    int Stock,
    bool IsActive,
    bool IsFeatured,
    int? CategoryId,
    string? CategoryName,
    string? ImageUrl,
    int VariantCount,
    DateTime CreatedAt);

public sealed record ProductVariantDto(
    int Id,
    string AttributeName,
    string AttributeValue,
    decimal PriceModifier,
    int Stock,
    string? Sku,
    bool IsActive);

public sealed record ProductImageDto(int Id, string Url, bool IsMain, int SortOrder);

/// <summary>Detalle completo de un producto para el formulario de edición.</summary>
public sealed record ProductDetailDto(
    int Id,
    string Name,
    string? Description,
    string? Sku,
    decimal Price,
    decimal? PriceOriginal,
    int Stock,
    bool IsActive,
    bool IsFeatured,
    int SortOrder,
    int? CategoryId,
    string? CategoryName,
    // Imagen principal (sincronizada con la galería).
    string? ImageUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // true si aparece en pedidos: al eliminarlo solo se desactiva.
    bool HasOrders,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductImageDto> Images);

// ── Escritura ─────────────────────────────────────────────────────────────────

/// <summary>Opción/variante del producto. Sin <c>Id</c> se crea; con <c>Id</c> se actualiza.</summary>
public sealed record ProductVariantUpsertRequest(
    int? Id,
    [Required(ErrorMessage = "El nombre de la opción es obligatorio."),
               StringLength(50, MinimumLength = 1, ErrorMessage = "La opción debe tener entre 1 y 50 caracteres.")]
    string AttributeName,
    [Required(ErrorMessage = "El valor de la opción es obligatorio."),
               StringLength(100, MinimumLength = 1, ErrorMessage = "El valor debe tener entre 1 y 100 caracteres.")]
    string AttributeValue,
    [Range(-ProductRules.MaxPrice, ProductRules.MaxPrice, ErrorMessage = "El ajuste de precio no es válido.")]
    decimal PriceModifier,
    [Range(0, int.MaxValue, ErrorMessage = "El stock de la variante no puede ser negativo.")]
    int Stock,
    [StringLength(ProductRules.SkuMaxLength, ErrorMessage = "El SKU de la variante admite como máximo 64 caracteres.")]
    string? Sku,
    bool IsActive = true);

/// <summary>Alta o edición de un producto (JSON).</summary>
public sealed record ProductUpsertRequest(
    [Required(ErrorMessage = "El nombre es obligatorio."),
               StringLength(ProductRules.NameMaxLength, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 150 caracteres.")]
    string Name,
    [StringLength(ProductRules.DescriptionMaxLength, ErrorMessage = "La descripción admite como máximo 2000 caracteres.")]
    string? Description,
    [StringLength(ProductRules.SkuMaxLength, ErrorMessage = "El SKU admite como máximo 64 caracteres.")]
    string? Sku,
    [Range(0, ProductRules.MaxPrice, ErrorMessage = "El precio debe ser mayor o igual a 0.")]
    decimal Price,
    [Range(0, ProductRules.MaxPrice, ErrorMessage = "El precio anterior debe ser mayor o igual a 0.")]
    decimal? PriceOriginal,
    [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
    int Stock,
    int? CategoryId,
    bool IsActive = true,
    bool IsFeatured = false,
    // Posición en el catálogo. Si no se envía se conserva la actual (o se añade al final al crear).
    int? SortOrder = null,
    // Reemplazo completo de variantes: las ausentes se eliminan. null = no modificar.
    [MaxLength(ProductRules.MaxVariants, ErrorMessage = "Un producto admite como máximo 100 variantes.")]
    IReadOnlyList<ProductVariantUpsertRequest>? Variants = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PriceOriginal is { } original && original < Price)
            yield return new ValidationResult("El precio anterior debe ser mayor o igual al precio actual.", [nameof(PriceOriginal)]);

        if (Variants is not { Count: > 0 }) yield break;

        var duplicated = Variants
            .Where(v => !string.IsNullOrWhiteSpace(v.AttributeName) && !string.IsNullOrWhiteSpace(v.AttributeValue))
            .GroupBy(v => (v.AttributeName.Trim().ToUpperInvariant(), v.AttributeValue.Trim().ToUpperInvariant()))
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicated != null)
        {
            var first = duplicated.First();
            yield return new ValidationResult(
                $"La variante \"{first.AttributeName.Trim()}: {first.AttributeValue.Trim()}\" está repetida.", [nameof(Variants)]);
        }

        var duplicatedSku = Variants
            .Select(v => v.Sku?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .GroupBy(s => s!.ToUpperInvariant())
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicatedSku != null)
            yield return new ValidationResult($"El SKU de variante \"{duplicatedSku.First()}\" está repetido.", [nameof(Variants)]);

        var duplicatedId = Variants.Where(v => v.Id is > 0).GroupBy(v => v.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicatedId != null)
            yield return new ValidationResult("Hay variantes repetidas en la solicitud.", [nameof(Variants)]);
    }
}

/// <summary>Nuevo orden de la galería. La primera imagen pasa a ser la principal.</summary>
public sealed record ProductImageOrderRequest(
    [Required(ErrorMessage = "Indica el orden de las imágenes."),
               MinLength(1, ErrorMessage = "Indica el orden de las imágenes.")]
    IReadOnlyList<int> ImageIds);

/// <summary>Resultado de eliminar un producto: si tiene pedidos solo se desactiva.</summary>
public sealed record ProductDeleteResultDto(bool Deleted, bool Deactivated, string Message);

public enum ProductBulkAction
{
    [JsonStringEnumMemberName("activate")] Activate,
    [JsonStringEnumMemberName("deactivate")] Deactivate,
    [JsonStringEnumMemberName("delete")] Delete,
    [JsonStringEnumMemberName("setCategory")] SetCategory,
}

/// <summary>Acción masiva sobre varios productos. Con <c>setCategory</c>, <c>CategoryId = null</c> quita la categoría.</summary>
public sealed record ProductBulkRequest(
    [Required(ErrorMessage = "Selecciona al menos un producto."),
               MinLength(1, ErrorMessage = "Selecciona al menos un producto."),
               MaxLength(500, ErrorMessage = "Puedes procesar como máximo 500 productos a la vez.")]
    IReadOnlyList<int> Ids,
    ProductBulkAction Action,
    int? CategoryId = null);

public sealed record ProductBulkResultDto(int Affected, int Deleted, int Deactivated, string Message);

// ── Categorías ────────────────────────────────────────────────────────────────

public sealed record ProductCategoryDto(int Id, string Name, string? Description, int ProductCount, DateTime CreatedAt);

public sealed record ProductCategoryUpsertRequest(
    [Required(ErrorMessage = "El nombre de la categoría es obligatorio."),
               StringLength(100, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 100 caracteres.")]
    string Name,
    [StringLength(500, ErrorMessage = "La descripción admite como máximo 500 caracteres.")]
    string? Description = null);
