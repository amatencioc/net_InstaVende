using BenchmarkDotNet.Attributes;
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
[MemoryDiagnoser]
[CPUUsageDiagnoser]
public class GetStatsBenchmark
{
    private AppDbContext _db = null !;
    private const int BusinessId = 1;
    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("GetStatsBenchmarkDb").Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        // Seed 500 conversations across statuses and channel types
        var channels = new[]
        {
            ChannelType.WhatsApp,
            ChannelType.Instagram,
            ChannelType.FacebookMessenger
        };
        var convStatuses = new[]
        {
            ConversationStatus.BotActive,
            ConversationStatus.Resolved,
            ConversationStatus.HumanActive
        };
        for (int i = 1; i <= 500; i++)
        {
            _db.Conversations.Add(new Conversation { Id = i, BusinessId = BusinessId, ContactId = i, ChannelType = channels[i % channels.Length], Status = convStatuses[i % convStatuses.Length], CreatedAt = now.AddDays(-(i % 40)), UpdatedAt = now });
        }

        // Seed 300 orders
        var orderStatuses = new[]
        {
            OrderStatus.Pending,
            OrderStatus.Delivered,
            OrderStatus.Cancelled,
            OrderStatus.Preparing
        };
        for (int i = 1; i <= 300; i++)
        {
            _db.Orders.Add(new Order { Id = i, BusinessId = BusinessId, ContactId = i, OrderNumber = $"ORD-{i:D5}", Status = orderStatuses[i % orderStatuses.Length], Total = 10m * i, Subtotal = 10m * i, CreatedAt = monthStart.AddDays(i % 28), UpdatedAt = now, ChannelType = ChannelType.WhatsApp });
        }

        // Seed 50 products
        for (int i = 1; i <= 50; i++)
        {
            _db.Products.Add(new Product { Id = i, BusinessId = BusinessId, Name = $"Producto {i}", IsActive = i % 5 != 0 });
        }

        _db.SaveChanges();
    }

    [GlobalCleanup]
    public void Cleanup() => _db.Dispose();
    // ---------- BASELINE: N roundtrips independientes ----------
    [Benchmark(Baseline = true, Description = "Dashboard: N queries secuenciales")]
    public async Task Dashboard_NQueries()
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var total = await _db.Conversations.CountAsync(c => c.BusinessId == BusinessId);
        var resolved = await _db.Conversations.CountAsync(c => c.BusinessId == BusinessId && c.Status == ConversationStatus.Resolved);
        var active = await _db.Conversations.CountAsync(c => c.BusinessId == BusinessId && c.Status == ConversationStatus.BotActive);
        var products = await _db.Products.CountAsync(p => p.BusinessId == BusinessId && p.IsActive);
        var byChannel = await _db.Conversations.AsNoTracking().Where(c => c.BusinessId == BusinessId).GroupBy(c => c.ChannelType).Select(g => new { Channel = g.Key, Count = g.Count() }).ToListAsync();
        var daily = await _db.Conversations.AsNoTracking().Where(c => c.BusinessId == BusinessId && c.CreatedAt >= since).GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month, c.CreatedAt.Day }).Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() }).OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day).ToListAsync();
    }

    // ---------- OPTIMIZADO: una sola query proyectada (Orders) ----------
    [Benchmark(Description = "Orders: 1 query agregada")]
    public async Task Orders_SingleQuery()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var stats = await _db.Orders.AsNoTracking().Where(o => o.BusinessId == BusinessId).GroupBy(_ => 1).Select(g => new { TotalMonth = g.Count(o => o.CreatedAt >= monthStart), RevenueMonth = g.Where(o => o.CreatedAt >= monthStart && o.Status != OrderStatus.Cancelled).Sum(o => (decimal? )o.Total) ?? 0m, Pending = g.Count(o => o.Status == OrderStatus.Pending), Delivered = g.Count(o => o.Status == OrderStatus.Delivered), }).FirstOrDefaultAsync();
    }

    // ---------- BASELINE Orders: 4 roundtrips ----------
    [Benchmark(Description = "Orders: 4 queries secuenciales")]
    public async Task Orders_NQueries()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var totalMonth = await _db.Orders.CountAsync(o => o.BusinessId == BusinessId && o.CreatedAt >= monthStart);
        var revenueMonth = await _db.Orders.Where(o => o.BusinessId == BusinessId && o.CreatedAt >= monthStart && o.Status != OrderStatus.Cancelled).SumAsync(o => (decimal? )o.Total) ?? 0m;
        var pending = await _db.Orders.CountAsync(o => o.BusinessId == BusinessId && o.Status == OrderStatus.Pending);
        var delivered = await _db.Orders.CountAsync(o => o.BusinessId == BusinessId && o.Status == OrderStatus.Delivered);
    }

    // ---------- OPTIMIZADO: Dashboard con query agregada de conversaciones ----------
    [Benchmark(Description = "Dashboard: query agregada + daily")]
    public async Task Dashboard_Optimized()
    {
        var since = DateTime.UtcNow.AddDays(-30);

        // 1 query: total + resolved + active en un solo GROUP BY
        var convStats = await _db.Conversations
            .AsNoTracking()
            .Where(c => c.BusinessId == BusinessId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total    = g.Count(),
                Resolved = g.Count(c => c.Status == ConversationStatus.Resolved),
                Active   = g.Count(c => c.Status == ConversationStatus.BotActive),
            })
            .FirstOrDefaultAsync();

        var products = await _db.Products.CountAsync(p => p.BusinessId == BusinessId && p.IsActive);

        // Agrupación por canal
        var byChannel = await _db.Conversations
            .AsNoTracking()
            .Where(c => c.BusinessId == BusinessId)
            .GroupBy(c => c.ChannelType)
            .Select(g => new { Channel = g.Key, Count = g.Count() })
            .ToListAsync();

        // Agrupación diaria filtrada por BusinessId + CreatedAt (beneficia del nuevo índice)
        var daily = await _db.Conversations
            .AsNoTracking()
            .Where(c => c.BusinessId == BusinessId && c.CreatedAt >= since)
            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month, c.CreatedAt.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day)
            .ToListAsync();
    }
}