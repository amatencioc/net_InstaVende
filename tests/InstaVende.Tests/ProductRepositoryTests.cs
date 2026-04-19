using FluentAssertions;
using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Tests;

public class ProductRepositoryTests
{
    private AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private async Task SeedProductsAsync(AppDbContext db)
    {
        var category = new ProductCategory { BusinessId = 1, Name = "Electrónicos" };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();

        db.Products.AddRange(
            new Product { BusinessId = 1, Name = "Laptop", Price = 999.99m, Stock = 5, IsActive = true },
            new Product { BusinessId = 1, Name = "Mouse inalámbrico", Price = 29.99m, Stock = 20, IsActive = true },
            new Product { BusinessId = 1, Name = "Teclado mecánico", Price = 79.99m, Stock = 0, IsActive = false },
            new Product { BusinessId = 2, Name = "Monitor", Price = 349.99m, Stock = 3, IsActive = true }
        );
        await db.SaveChangesAsync();

        // Associate products with category after save
        var laptop = await db.Products.FirstAsync(p => p.Name == "Laptop");
        var mouse = await db.Products.FirstAsync(p => p.Name == "Mouse inalámbrico");
        laptop.CategoryId = category.Id;
        mouse.CategoryId = category.Id;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetByBusinessId_ActiveOnly_ReturnsOnlyActiveForBusiness()
    {
        using var db = CreateDb();
        await SeedProductsAsync(db);

        var repo = new ProductRepository(db);
        var result = (await repo.GetByBusinessIdAsync(1, activeOnly: true)).ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.IsActive && p.BusinessId == 1);
    }

    [Fact]
    public async Task GetByBusinessId_IncludeInactive_ReturnsAllForBusiness()
    {
        using var db = CreateDb();
        await SeedProductsAsync(db);

        var repo = new ProductRepository(db);
        var result = (await repo.GetByBusinessIdAsync(1, activeOnly: false)).ToList();

        result.Should().HaveCount(3);
        result.Should().OnlyContain(p => p.BusinessId == 1);
    }

    [Fact]
    public async Task SearchAsync_ByName_ReturnsMatchingProducts()
    {
        using var db = CreateDb();
        await SeedProductsAsync(db);

        var repo = new ProductRepository(db);
        var result = (await repo.SearchAsync(1, "Laptop", null)).ToList();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Laptop");
    }

    [Fact]
    public async Task SearchAsync_ByCategoryId_ReturnsMatchingProducts()
    {
        using var db = CreateDb();
        await SeedProductsAsync(db);

        var category = await db.ProductCategories.FirstAsync();
        var repo = new ProductRepository(db);
        var result = (await repo.SearchAsync(1, null, category.Id)).ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.CategoryId == category.Id);
    }

    [Fact]
    public async Task SearchAsync_EmptySearch_ReturnsAllActiveForBusiness()
    {
        using var db = CreateDb();
        await SeedProductsAsync(db);

        var repo = new ProductRepository(db);
        var result = (await repo.SearchAsync(1, null, null)).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddAsync_AddsProductSuccessfully()
    {
        using var db = CreateDb();
        var repo = new ProductRepository(db);

        var product = new Product
        {
            BusinessId = 10,
            Name = "Nuevo Producto",
            Price = 50.00m,
            Stock = 10,
            IsActive = true
        };

        await repo.AddAsync(product);
        await db.SaveChangesAsync();

        var saved = await db.Products.FindAsync(product.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Nuevo Producto");
    }

    [Fact]
    public async Task DeleteAsync_RemovesProduct()
    {
        using var db = CreateDb();
        await SeedProductsAsync(db);

        var repo = new ProductRepository(db);
        var products = (await repo.GetByBusinessIdAsync(1, false)).ToList();
        var toDelete = products.First();

        await repo.DeleteAsync(toDelete);
        await db.SaveChangesAsync();

        var remaining = await db.Products.FindAsync(toDelete.Id);
        remaining.Should().BeNull();
    }
}
