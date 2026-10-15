using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Products;

/// <summary>Categorías del catálogo (nombre único por negocio).</summary>
[Route("api/product-categories")]
public sealed class ProductCategoriesController(AppDbContext db) : TenantController
{
    /// <summary>Todas las categorías del negocio ordenadas por nombre, con su número de productos.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductCategoryDto>>> List(CancellationToken ct)
        => await db.ProductCategories.AsNoTracking()
            .Where(c => c.BusinessId == BusinessId)
            .OrderBy(c => c.Name)
            .Select(c => new ProductCategoryDto(c.Id, c.Name, c.Description, c.Products.Count, c.CreatedAt))
            .ToListAsync(ct);

    [HttpPost]
    [ProducesResponseType<ProductCategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductCategoryDto>> Create(ProductCategoryUpsertRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await NameTakenAsync(name, null, ct)) return ConflictProblem($"Ya existe una categoría llamada \"{name}\".");

        var category = new ProductCategory
        {
            BusinessId = BusinessId, Name = name, Description = Clean(request.Description), CreatedAt = DateTime.UtcNow,
        };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(List), null,
            new ProductCategoryDto(category.Id, category.Name, category.Description, 0, category.CreatedAt));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ProductCategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductCategoryDto>> Update(int id, ProductCategoryUpsertRequest request, CancellationToken ct)
    {
        var category = await db.ProductCategories.FirstOrDefaultAsync(c => c.Id == id && c.BusinessId == BusinessId, ct);
        if (category == null) return NotFoundProblem("La categoría no existe o fue eliminada.");

        var name = request.Name.Trim();
        if (await NameTakenAsync(name, id, ct)) return ConflictProblem($"Ya existe una categoría llamada \"{name}\".");

        category.Name = name;
        category.Description = Clean(request.Description);
        await db.SaveChangesAsync(ct);

        var count = await db.Products.CountAsync(p => p.BusinessId == BusinessId && p.CategoryId == id, ct);
        return new ProductCategoryDto(category.Id, category.Name, category.Description, count, category.CreatedAt);
    }

    /// <summary>Elimina la categoría; sus productos quedan sin categoría.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!await db.ProductCategories.AnyAsync(c => c.Id == id && c.BusinessId == BusinessId, ct))
            return NotFoundProblem("La categoría no existe o fue eliminada.");

        // La FK no tiene SET NULL en la BD: se desasignan los productos y se borra en una transacción
        // (dentro de la estrategia de reintentos configurada para SQL Server).
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Products
                .Where(p => p.BusinessId == BusinessId && p.CategoryId == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.CategoryId, (int?)null)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);
            await db.ProductCategories.Where(c => c.Id == id && c.BusinessId == BusinessId).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
        });
        return NoContent();
    }

    private Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken ct)
        => db.ProductCategories.AnyAsync(c => c.BusinessId == BusinessId && c.Name == name && c.Id != (exceptId ?? 0), ct);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
