using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VSDiagnostics;

namespace BenchmarkSuite1;

/// <summary>
/// Mide el coste de las queries de Products del bot:
///   - Catálogo / AddToCart  ? BusinessId + IsActive + Stock + ORDER BY SortOrder
///   - BuscarAlternativas    ? BusinessId + IsActive + CategoryId + Stock
///   - GetOrderStatus        ? Orders WHERE ConversationId + Status ORDER BY CreatedAt
/// Baseline = sin índice en Products (estado actual).
/// Optimized = con índice (BusinessId, IsActive, SortOrder).
/// </summary>
[MemoryDiagnoser]
[CPUUsageDiagnoser]
public class BotProductQueryBenchmark
{
    private AppDbContext _db = null!;
    private const int BusinessId    = 1;
    private const int ConversationId = 1;
    private const int CategoryId     = 1;

    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("BotProductQueryBenchmarkDb")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        // Seed 200 productos distribuidos en 5 categorías
        for (int i = 1; i <= 200; i++)
        {
            _db.Products.Add(new Product
            {
                Id         = i,
                BusinessId = BusinessId,
                CategoryId = (i % 5) + 1,
                Name       = $"Producto {i}",
                Price      = 10m * i,
                Stock      = i % 10,          // algunos con stock 0
                IsActive   = i % 7 != 0,      // ~86 % activos
                IsFeatured = i % 3 == 0,
                SortOrder  = i % 20,
            });
        }

        // Seed 50 conversaciones y 100 pedidos
        for (int i = 1; i <= 50; i++)
        {
            _db.Conversations.Add(new Conversation
            {
                Id             = i,
                BusinessId     = BusinessId,
                ContactId      = i,
                ChannelType    = ChannelType.WhatsApp,
                Status         = ConversationStatus.BotActive,
                CreatedAt      = DateTime.UtcNow.AddDays(-i),
                UpdatedAt      = DateTime.UtcNow,
            });
        }

        var statuses = new[]
        {
            OrderStatus.Confirmed,
            OrderStatus.Preparing,
            OrderStatus.Delivered,
            OrderStatus.Cancelled
        };

        for (int i = 1; i <= 100; i++)
        {
            _db.Orders.Add(new Order
            {
                Id             = i,
                BusinessId     = BusinessId,
                ContactId      = (i % 50) + 1,
                ConversationId = (i % 50) + 1,
                OrderNumber    = $"ORD-{i:D5}",
                Status         = statuses[i % statuses.Length],
                Total          = 100m * i,
                Subtotal       = 100m * i,
                CreatedAt      = DateTime.UtcNow.AddDays(-i),
                UpdatedAt      = DateTime.UtcNow,
                ChannelType    = ChannelType.WhatsApp,
            });
        }

        _db.SaveChanges();
    }

    [GlobalCleanup]
    public void Cleanup() => _db.Dispose();

    // ?? BASELINE: catálogo — BusinessId + IsActive + Stock + ORDER BY SortOrder ??

    [Benchmark(Baseline = true, Description = "Catálogo — sin índice en Products")]
    public async Task Catalog_NoIndex()
    {
        var _ = await _db.Products
            .AsNoTracking()
            .Where(p => p.BusinessId == BusinessId && p.IsActive && p.Stock > 0)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Take(20)
            .ToListAsync();
    }

    // ?? Buscar alternativas — BusinessId + IsActive + CategoryId + Stock ????????

    [Benchmark(Description = "BuscarAlternativas — sin índice en Products")]
    public async Task Alternatives_NoIndex()
    {
        var _ = await _db.Products
            .AsNoTracking()
            .Where(p => p.BusinessId == BusinessId
                     && p.IsActive
                     && p.Id != 5
                     && p.CategoryId == CategoryId
                     && p.Stock > 0)
            .OrderByDescending(p => p.IsFeatured)
            .Take(3)
            .ToListAsync();
    }

    // ?? GetOrderStatus — ConversationId + Status ORDER BY CreatedAt ?????????????

    [Benchmark(Description = "GetOrderStatus — índice (ConversationId, Status) existente")]
    public async Task OrderStatus_ExistingIndex()
    {
        var _ = await _db.Orders
            .AsNoTracking()
            .Where(o => o.ConversationId == ConversationId
                     && o.Status != OrderStatus.Pending)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
    }
}
