using System.Linq.Expressions;
using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using InstaVende.Web.Features.Shared;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Products;

/// <summary>
/// Catálogo de productos del negocio: listado paginado, alta/edición con variantes,
/// galería de imágenes, duplicado, borrado (lógico si tiene pedidos) y acciones masivas.
/// </summary>
[Route("api/products")]
public sealed class ProductsController(AppDbContext db, ImageService images, ILogger<ProductsController> logger) : TenantController
{
    /// <summary>6 imágenes de 5 MB más el sobrecoste del multipart.</summary>
    private const long MaxUploadBytes = ProductRules.MaxImages * ImageService.MaxBytes + 2 * 1024 * 1024;

    // ── Lectura ──────────────────────────────────────────────────────────────

    /// <summary>Listado paginado con búsqueda (nombre o SKU), filtros y ordenación en SQL.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> List(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] ProductStatusFilter? status,
        [FromQuery] ProductSort? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var query = db.Products.AsNoTracking().Where(p => p.BusinessId == BusinessId);

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(p => p.Name.Contains(term) || (p.Sku != null && p.Sku.Contains(term)));

        if (categoryId is { } catId)
            query = catId == 0 ? query.Where(p => p.CategoryId == null) : query.Where(p => p.CategoryId == catId);

        query = status switch
        {
            ProductStatusFilter.Active => query.Where(p => p.IsActive),
            ProductStatusFilter.Inactive => query.Where(p => !p.IsActive),
            ProductStatusFilter.LowStock => query.Where(p => p.Stock > 0 && p.Stock <= ProductRules.LowStockThreshold),
            ProductStatusFilter.OutOfStock => query.Where(p => p.Stock <= 0),
            _ => query,
        };

        query = (sort ?? ProductSort.Newest) switch
        {
            ProductSort.Oldest => query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            ProductSort.NameAsc => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            ProductSort.NameDesc => query.OrderByDescending(p => p.Name).ThenByDescending(p => p.Id),
            ProductSort.PriceAsc => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            ProductSort.PriceDesc => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.Id),
            ProductSort.StockAsc => query.OrderBy(p => p.Stock).ThenBy(p => p.Id),
            ProductSort.StockDesc => query.OrderByDescending(p => p.Stock).ThenByDescending(p => p.Id),
            ProductSort.Manual => query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ThenBy(p => p.Id),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
        };

        return await query.Select(ListProjection).ToPagedResultAsync(PageRequest.From(page, pageSize), ct);
    }

    /// <summary>Detalle con variantes e imágenes.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailDto>> Get(int id, CancellationToken ct)
    {
        var dto = await LoadDetailAsync(id, ct);
        return dto is null ? NotFoundProblem("El producto no existe o fue eliminado.") : dto;
    }

    // ── Alta / edición ───────────────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDetailDto>> Create(ProductUpsertRequest request, CancellationToken ct)
    {
        var error = await ValidateReferencesAsync(request, productId: null, ct);
        if (error != null) return error;

        var sortOrder = request.SortOrder
            ?? (await db.Products.Where(p => p.BusinessId == BusinessId).MaxAsync(p => (int?)p.SortOrder, ct) ?? 0) + 1;

        var product = new Product { BusinessId = BusinessId, SortOrder = sortOrder, CreatedAt = DateTime.UtcNow };
        Apply(product, request);
        foreach (var v in request.Variants ?? [])
            product.Variants.Add(ApplyVariant(new ProductVariant(), v));

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        var dto = (await LoadDetailAsync(product.Id, ct))!;
        return CreatedAtAction(nameof(Get), new { id = product.Id }, dto);
    }

    /// <summary>
    /// Actualiza el producto. Las variantes enviadas reemplazan a las actuales
    /// (crear/actualizar/eliminar) en un único <c>SaveChanges</c>, que es transaccional.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDetailDto>> Update(int id, ProductUpsertRequest request, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == BusinessId, ct);
        if (product == null) return NotFoundProblem("El producto no existe o fue eliminado.");

        var error = await ValidateReferencesAsync(request, id, ct);
        if (error != null) return error;

        Apply(product, request);
        if (request.SortOrder is { } sortOrder) product.SortOrder = sortOrder;
        product.UpdatedAt = DateTime.UtcNow;

        if (request.Variants != null)
        {
            var existing = product.Variants.ToDictionary(v => v.Id);
            foreach (var unknown in request.Variants.Where(v => v.Id is > 0 && !existing.ContainsKey(v.Id.Value)))
                return BadRequestProblem($"La variante {unknown.AttributeName}: {unknown.AttributeValue} no pertenece a este producto.");

            var keep = request.Variants.Where(v => v.Id is > 0).Select(v => v.Id!.Value).ToHashSet();
            foreach (var removed in product.Variants.Where(v => !keep.Contains(v.Id)).ToList())
                db.ProductVariants.Remove(removed);

            foreach (var v in request.Variants)
            {
                if (v.Id is > 0) ApplyVariant(existing[v.Id.Value], v);
                else product.Variants.Add(ApplyVariant(new ProductVariant(), v));
            }
        }

        await db.SaveChangesAsync(ct);
        return (await LoadDetailAsync(id, ct))!;
    }

    /// <summary>Crea una copia (no disponible y sin destacar) con sus variantes e imágenes.</summary>
    [HttpPost("{id:int}/duplicate")]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailDto>> Duplicate(int id, CancellationToken ct)
    {
        var source = await db.Products.AsNoTracking()
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == BusinessId, ct);
        if (source == null) return NotFoundProblem("El producto no existe o fue eliminado.");

        const string suffix = " (copia)";
        var name = source.Name.Length + suffix.Length > ProductRules.NameMaxLength
            ? source.Name[..(ProductRules.NameMaxLength - suffix.Length)] + suffix
            : source.Name + suffix;

        var copy = new Product
        {
            BusinessId = BusinessId,
            CategoryId = source.CategoryId,
            Name = name,
            Description = source.Description,
            Sku = await NextFreeSkuAsync(source.Sku, ct),
            Price = source.Price,
            PriceOriginal = source.PriceOriginal,
            Stock = source.Stock,
            ImageUrl = source.ImageUrl,
            IsActive = false,
            IsFeatured = false,
            SortOrder = source.SortOrder,
            CreatedAt = DateTime.UtcNow,
        };
        foreach (var v in source.Variants.OrderBy(v => v.Id))
            copy.Variants.Add(new ProductVariant
            {
                AttributeName = v.AttributeName, AttributeValue = v.AttributeValue, PriceModifier = v.PriceModifier,
                Stock = v.Stock, Sku = v.Sku, IsActive = v.IsActive,
            });
        // Las imágenes comparten archivo: solo se borra del disco cuando ningún producto lo referencia.
        foreach (var img in source.Images.OrderBy(i => i.SortOrder))
            copy.Images.Add(new ProductImage { Url = img.Url, IsMain = img.IsMain, SortOrder = img.SortOrder });

        db.Products.Add(copy);
        await db.SaveChangesAsync(ct);

        var dto = (await LoadDetailAsync(copy.Id, ct))!;
        return CreatedAtAction(nameof(Get), new { id = copy.Id }, dto);
    }

    // ── Borrado ──────────────────────────────────────────────────────────────

    /// <summary>Elimina el producto. Si figura en pedidos se desactiva para conservar el historial.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType<ProductDeleteResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDeleteResultDto>> Delete(int id, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == BusinessId, ct);
        if (product == null) return NotFoundProblem("El producto no existe o fue eliminado.");

        if (await db.OrderItems.AnyAsync(i => i.ProductId == id, ct))
        {
            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new ProductDeleteResultDto(false, true,
                "El producto tiene pedidos asociados: se marcó como no disponible para conservar el historial.");
        }

        var files = CollectFiles([product]);
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        await DeleteUnreferencedFilesAsync(files, ct);
        return new ProductDeleteResultDto(true, false, "Producto eliminado.");
    }

    // ── Acciones masivas ─────────────────────────────────────────────────────

    [HttpPost("bulk")]
    [ProducesResponseType<ProductBulkResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductBulkResultDto>> Bulk(ProductBulkRequest request, CancellationToken ct)
    {
        var ids = request.Ids.Distinct().ToList();

        if (request.Action == ProductBulkAction.SetCategory && request.CategoryId is { } catId
            && !await db.ProductCategories.AnyAsync(c => c.Id == catId && c.BusinessId == BusinessId, ct))
            return BadRequestProblem("La categoría seleccionada no existe.");

        var productsQuery = db.Products.Where(p => p.BusinessId == BusinessId && ids.Contains(p.Id));
        if (request.Action == ProductBulkAction.Delete) productsQuery = productsQuery.Include(p => p.Images);
        var products = await productsQuery.ToListAsync(ct);
        if (products.Count == 0) return new ProductBulkResultDto(0, 0, 0, "No se encontró ninguno de los productos seleccionados.");

        var now = DateTime.UtcNow;
        switch (request.Action)
        {
            case ProductBulkAction.Activate:
            case ProductBulkAction.Deactivate:
                var active = request.Action == ProductBulkAction.Activate;
                foreach (var p in products.Where(p => p.IsActive != active)) { p.IsActive = active; p.UpdatedAt = now; }
                await db.SaveChangesAsync(ct);
                return new ProductBulkResultDto(products.Count, 0, 0, active
                    ? $"{Plural(products.Count, "producto activado", "productos activados")}."
                    : $"{Plural(products.Count, "producto desactivado", "productos desactivados")}.");

            case ProductBulkAction.SetCategory:
                foreach (var p in products) { p.CategoryId = request.CategoryId; p.UpdatedAt = now; }
                await db.SaveChangesAsync(ct);
                return new ProductBulkResultDto(products.Count, 0, 0, request.CategoryId == null
                    ? $"Se quitó la categoría a {Plural(products.Count, "producto", "productos")}."
                    : $"Categoría asignada a {Plural(products.Count, "producto", "productos")}.");

            case ProductBulkAction.Delete:
                var foundIds = products.Select(p => p.Id).ToList();
                var withOrders = (await db.OrderItems
                        .Where(i => foundIds.Contains(i.ProductId))
                        .Select(i => i.ProductId)
                        .Distinct()
                        .ToListAsync(ct))
                    .ToHashSet();

                var toDelete = products.Where(p => !withOrders.Contains(p.Id)).ToList();
                var toDeactivate = products.Where(p => withOrders.Contains(p.Id)).ToList();
                foreach (var p in toDeactivate) { p.IsActive = false; p.UpdatedAt = now; }
                var files = CollectFiles(toDelete);
                db.Products.RemoveRange(toDelete);
                await db.SaveChangesAsync(ct);
                await DeleteUnreferencedFilesAsync(files, ct);

                var message = toDeactivate.Count == 0
                    ? $"{Plural(toDelete.Count, "producto eliminado", "productos eliminados")}."
                    : $"{Plural(toDelete.Count, "producto eliminado", "productos eliminados")}; " +
                      $"{Plural(toDeactivate.Count, "producto con pedidos se desactivó", "productos con pedidos se desactivaron")}.";
                return new ProductBulkResultDto(products.Count, toDelete.Count, toDeactivate.Count, message);

            default:
                return BadRequestProblem("Acción no soportada.");
        }
    }

    // ── Imágenes ─────────────────────────────────────────────────────────────

    /// <summary>Sube una o varias imágenes (máx. 6 por producto, 5 MB cada una).</summary>
    [HttpPost("{id:int}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    [ProducesResponseType<IReadOnlyList<ProductImageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProductImageDto>>> UploadImages(int id, IFormFileCollection files, CancellationToken ct)
    {
        var product = await LoadWithImagesAsync(id, ct);
        if (product == null) return NotFoundProblem("El producto no existe o fue eliminado.");
        if (files.Count == 0) return BadRequestProblem("Selecciona al menos una imagen.");

        AdoptLegacyImage(product);
        var available = ProductRules.MaxImages - product.Images.Count;
        if (files.Count > available)
            return BadRequestProblem(available <= 0
                ? $"El producto ya tiene el máximo de {ProductRules.MaxImages} imágenes."
                : $"Solo puedes añadir {Plural(available, "imagen más", "imágenes más")} (máximo {ProductRules.MaxImages}).");

        var saved = new List<string>();
        foreach (var file in files)
        {
            var result = await images.SaveAsync(file, BusinessId, "products", ct);
            if (!result.Success)
            {
                saved.ForEach(images.Delete);
                return BadRequestProblem(files.Count > 1 ? $"{file.FileName}: {result.Error}" : result.Error!);
            }
            saved.Add(result.Url!);
        }

        var next = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1;
        foreach (var url in saved)
            product.Images.Add(new ProductImage { Url = url, SortOrder = next++ });

        NormalizeImages(product, product.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList());
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            saved.ForEach(images.Delete);
            throw;
        }
        return MapImages(product);
    }

    /// <summary>Reordena la galería. Debe incluir todas las imágenes; la primera queda como principal.</summary>
    [HttpPut("{id:int}/images/order")]
    [ProducesResponseType<IReadOnlyList<ProductImageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProductImageDto>>> ReorderImages(int id, ProductImageOrderRequest request, CancellationToken ct)
    {
        var product = await LoadWithImagesAsync(id, ct);
        if (product == null) return NotFoundProblem("El producto no existe o fue eliminado.");

        var byId = product.Images.ToDictionary(i => i.Id);
        var requested = request.ImageIds.Distinct().ToList();
        if (requested.Count != byId.Count || requested.Any(i => !byId.ContainsKey(i)))
            return BadRequestProblem("La lista de imágenes no coincide con la galería del producto. Recarga la página.");

        NormalizeImages(product, requested.Select(i => byId[i]).ToList());
        await db.SaveChangesAsync(ct);
        return MapImages(product);
    }

    /// <summary>Marca una imagen como principal (la mueve a la primera posición).</summary>
    [HttpPut("{id:int}/images/{imageId:int}/main")]
    [ProducesResponseType<IReadOnlyList<ProductImageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProductImageDto>>> SetMainImage(int id, int imageId, CancellationToken ct)
    {
        var product = await LoadWithImagesAsync(id, ct);
        if (product == null) return NotFoundProblem("El producto no existe o fue eliminado.");
        var main = product.Images.FirstOrDefault(i => i.Id == imageId);
        if (main == null) return NotFoundProblem("La imagen no existe o fue eliminada.");

        var ordered = product.Images.Where(i => i.Id != imageId).OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Prepend(main).ToList();
        NormalizeImages(product, ordered);
        await db.SaveChangesAsync(ct);
        return MapImages(product);
    }

    /// <summary>Elimina una imagen de la galería y su archivo.</summary>
    [HttpDelete("{id:int}/images/{imageId:int}")]
    [ProducesResponseType<IReadOnlyList<ProductImageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProductImageDto>>> DeleteImage(int id, int imageId, CancellationToken ct)
    {
        var product = await LoadWithImagesAsync(id, ct);
        if (product == null) return NotFoundProblem("El producto no existe o fue eliminado.");
        var image = product.Images.FirstOrDefault(i => i.Id == imageId);
        if (image == null) return NotFoundProblem("La imagen no existe o fue eliminada.");

        var url = image.Url;
        product.Images.Remove(image);
        db.ProductImages.Remove(image);
        NormalizeImages(product, product.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList());
        await db.SaveChangesAsync(ct);
        await DeleteUnreferencedFilesAsync([url], ct);
        return MapImages(product);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly Expression<Func<Product, ProductListItemDto>> ListProjection = p => new ProductListItemDto(
        p.Id, p.Name, p.Sku, p.Price, p.PriceOriginal, p.Stock, p.IsActive, p.IsFeatured,
        p.CategoryId, p.Category != null ? p.Category.Name : null,
        p.ImageUrl ?? p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
        p.Variants.Count, p.CreatedAt);

    private Task<ProductDetailDto?> LoadDetailAsync(int id, CancellationToken ct) => db.Products.AsNoTracking()
        .Where(p => p.Id == id && p.BusinessId == BusinessId)
        .Select(p => new ProductDetailDto(
            p.Id, p.Name, p.Description, p.Sku, p.Price, p.PriceOriginal, p.Stock, p.IsActive, p.IsFeatured, p.SortOrder,
            p.CategoryId, p.Category != null ? p.Category.Name : null, p.ImageUrl, p.CreatedAt, p.UpdatedAt,
            p.OrderItems.Any(),
            p.Variants.OrderBy(v => v.Id)
                .Select(v => new ProductVariantDto(v.Id, v.AttributeName, v.AttributeValue, v.PriceModifier, v.Stock, v.Sku, v.IsActive))
                .ToList(),
            p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new ProductImageDto(i.Id, i.Url, i.IsMain, i.SortOrder))
                .ToList()))
        .AsSplitQuery()
        .FirstOrDefaultAsync(ct);

    private Task<Product?> LoadWithImagesAsync(int id, CancellationToken ct) => db.Products
        .Include(p => p.Images)
        .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == BusinessId, ct);

    /// <summary>Valida categoría y SKU contra el negocio. Devuelve el error o null si todo es correcto.</summary>
    private async Task<ActionResult?> ValidateReferencesAsync(ProductUpsertRequest request, int? productId, CancellationToken ct)
    {
        if (request.CategoryId is { } catId
            && !await db.ProductCategories.AnyAsync(c => c.Id == catId && c.BusinessId == BusinessId, ct))
        {
            ModelState.AddModelError(nameof(request.CategoryId), "La categoría seleccionada no existe.");
            return ValidationProblem(ModelState);
        }

        var sku = Clean(request.Sku);
        if (sku != null && await db.Products.AnyAsync(p => p.BusinessId == BusinessId && p.Sku == sku && p.Id != (productId ?? 0), ct))
            return ConflictProblem($"Ya existe otro producto con el SKU \"{sku}\".");

        return null;
    }

    private static void Apply(Product product, ProductUpsertRequest r)
    {
        product.Name = r.Name.Trim();
        product.Description = Clean(r.Description);
        product.Sku = Clean(r.Sku);
        product.Price = r.Price;
        product.PriceOriginal = r.PriceOriginal;
        product.Stock = r.Stock;
        product.CategoryId = r.CategoryId;
        product.IsActive = r.IsActive;
        product.IsFeatured = r.IsFeatured;
    }

    private static ProductVariant ApplyVariant(ProductVariant variant, ProductVariantUpsertRequest r)
    {
        variant.AttributeName = r.AttributeName.Trim();
        variant.AttributeValue = r.AttributeValue.Trim();
        variant.PriceModifier = r.PriceModifier;
        variant.Stock = r.Stock;
        variant.Sku = Clean(r.Sku);
        variant.IsActive = r.IsActive;
        return variant;
    }

    /// <summary>Aplica el orden indicado: SortOrder consecutivo, la primera es la principal y se sincroniza Product.ImageUrl.</summary>
    private static void NormalizeImages(Product product, IReadOnlyList<ProductImage> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SortOrder = i;
            ordered[i].IsMain = i == 0;
        }
        var mainUrl = ordered.Count > 0 ? ordered[0].Url : null;
        if (product.ImageUrl != mainUrl)
        {
            product.ImageUrl = mainUrl;
            product.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Productos anteriores a la galería solo tenían ImageUrl: se incorpora como primera imagen.</summary>
    private static void AdoptLegacyImage(Product product)
    {
        if (product.Images.Count == 0 && !string.IsNullOrWhiteSpace(product.ImageUrl))
            product.Images.Add(new ProductImage { Url = product.ImageUrl, IsMain = true, SortOrder = 0 });
    }

    private static List<ProductImageDto> MapImages(Product product) => product.Images
        .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
        .Select(i => new ProductImageDto(i.Id, i.Url, i.IsMain, i.SortOrder))
        .ToList();

    private static HashSet<string> CollectFiles(IEnumerable<Product> products)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in products)
        {
            if (!string.IsNullOrWhiteSpace(p.ImageUrl)) files.Add(p.ImageUrl);
            foreach (var i in p.Images) files.Add(i.Url);
        }
        return files;
    }

    /// <summary>Borra del disco los archivos que ya no referencia ningún producto (las copias comparten archivo).</summary>
    private async Task DeleteUnreferencedFilesAsync(IReadOnlyCollection<string> urls, CancellationToken ct)
    {
        if (urls.Count == 0) return;
        try
        {
            var list = urls.ToList();
            var stillUsed = new HashSet<string>(
                await db.ProductImages.Where(i => list.Contains(i.Url)).Select(i => i.Url)
                    .Concat(db.Products.Where(p => p.ImageUrl != null && list.Contains(p.ImageUrl)).Select(p => p.ImageUrl!))
                    .ToListAsync(ct),
                StringComparer.Ordinal);
            foreach (var url in list.Where(u => !stillUsed.Contains(u))) images.Delete(url);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // El registro ya se guardó: un archivo huérfano no debe convertir la operación en error.
            logger.LogWarning(ex, "No se pudieron limpiar las imágenes del negocio {BusinessId}", BusinessId);
        }
    }

    /// <summary>SKU libre para una copia: "ABC-1" → "ABC-1-2", "ABC-1-3"…</summary>
    private async Task<string?> NextFreeSkuAsync(string? sku, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        var baseSku = sku.Length > ProductRules.SkuMaxLength - 4 ? sku[..(ProductRules.SkuMaxLength - 4)] : sku;
        var prefix = baseSku + "-";
        var taken = (await db.Products.AsNoTracking()
                .Where(p => p.BusinessId == BusinessId && p.Sku != null && p.Sku.StartsWith(prefix))
                .Select(p => p.Sku!)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var n = 2; n < 1000; n++)
        {
            var candidate = prefix + n;
            if (!taken.Contains(candidate)) return candidate;
        }
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Plural(int count, string singular, string plural) => $"{count} {(count == 1 ? singular : plural)}";
}
