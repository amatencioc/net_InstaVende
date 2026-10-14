using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Shared;

/// <summary>Página de resultados estándar de la API.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Parámetros de paginación normalizados (page &gt;= 1, 1 &lt;= pageSize &lt;= 100).</summary>
public readonly record struct PageRequest(int Page, int PageSize)
{
    public static PageRequest From(int? page, int? pageSize, int defaultSize = 20)
        => new(Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? defaultSize, 1, 100));

    public int Skip => (Page - 1) * PageSize;
}

public static class PagingExtensions
{
    /// <summary>Cuenta y pagina una consulta ya ordenada en dos consultas SQL.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageRequest page, CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }
}
