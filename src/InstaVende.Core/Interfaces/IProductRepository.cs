using InstaVende.Core.Entities;
namespace InstaVende.Core.Interfaces;
public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetByBusinessIdAsync(int businessId, bool activeOnly = true);
    Task<IEnumerable<Product>> SearchAsync(int businessId, string? search, int? categoryId);
}
