using InstaVende.Core.Entities;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Infrastructure.Repositories;

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Product>> GetByBusinessIdAsync(int businessId, bool activeOnly = true)
    {
        var query = _context.Products.Include(p => p.Category).Where(p => p.BusinessId == businessId);
        if (activeOnly) query = query.Where(p => p.IsActive);
        return await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<Product>> SearchAsync(int businessId, string? search, int? categoryId)
    {
        var query = _context.Products.Include(p => p.Category)
            .Where(p => p.BusinessId == businessId && p.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
        return await query.OrderBy(p => p.Name).ToListAsync();
    }
}
