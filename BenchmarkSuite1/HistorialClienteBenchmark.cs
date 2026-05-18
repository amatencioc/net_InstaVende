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
[CPUUsageDiagnoser]
public class HistorialClienteBenchmark
{
    private AppDbContext _db = null !;
    private const int BusinessId = 1;
    private const string ExternalId = "521234567890";
    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("HistorialClienteBenchmarkDb").Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        var now = DateTime.UtcNow;
        _db.Contacts.Add(new Contact { Id = 1, BusinessId = BusinessId, ChannelType = ChannelType.WhatsApp, ExternalId = ExternalId, Name = "Test User", FirstSeenAt = now.AddDays(-60), LastSeenAt = now.AddDays(-1), });
        for (int i = 1; i <= 50; i++)
        {
            _db.Conversations.Add(new Conversation { Id = i, BusinessId = BusinessId, ContactId = 1, ChannelType = ChannelType.WhatsApp, Status = ConversationStatus.Resolved, CreatedAt = now.AddDays(-i), UpdatedAt = now.AddDays(-i), });
        }

        _db.SaveChanges();
    }

    [GlobalCleanup]
    public void Cleanup() => _db.Dispose();
    // ────── BASELINE: 3 roundtrips secuenciales ──────
    [Benchmark(Baseline = true, Description = "Historial: 3 queries secuenciales")]
    public async Task Historial_3Queries()
    {
        var contact = await _db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.ExternalId == ExternalId && c.BusinessId == BusinessId);
        if (contact == null)
            return;
        var totalConvs = await _db.Conversations.CountAsync(c => c.ContactId == contact.Id);
        var ultimaConv = await _db.Conversations.AsNoTracking().Where(c => c.ContactId == contact.Id).OrderByDescending(c => c.UpdatedAt).Select(c => c.UpdatedAt).FirstOrDefaultAsync();
    }

    // ────── OPTIMIZADO: 1 query con proyección ──────
    [Benchmark(Description = "Historial: 1 query con proyección")]
    public async Task Historial_SingleQuery()
    {
        var result = await _db.Contacts.AsNoTracking().Where(c => c.ExternalId == ExternalId && c.BusinessId == BusinessId).Select(c => new { c.Name, c.FirstSeenAt, c.LastSeenAt, TotalConversaciones = c.Conversations.Count(), UltimaConversacion = c.Conversations.OrderByDescending(cv => cv.UpdatedAt).Select(cv => cv.UpdatedAt).FirstOrDefault(), }).FirstOrDefaultAsync();
    }
}