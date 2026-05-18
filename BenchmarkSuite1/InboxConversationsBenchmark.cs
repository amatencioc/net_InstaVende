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
public class InboxConversationsBenchmark
{
    private AppDbContext _db = null !;
    private const int BusinessId = 1;
    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("InboxConversationsBenchmarkDb").Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        var now = DateTime.UtcNow;
        var channels = new[]
        {
            ChannelType.WhatsApp,
            ChannelType.Instagram,
            ChannelType.FacebookMessenger
        };
        var statuses = new[]
        {
            ConversationStatus.BotActive,
            ConversationStatus.Resolved,
            ConversationStatus.HumanActive
        };
        for (int i = 1; i <= 1000; i++)
        {
            _db.Conversations.Add(new Conversation { Id = i, BusinessId = BusinessId, ContactId = i, ChannelType = channels[i % channels.Length], Status = statuses[i % statuses.Length], CreatedAt = now.AddDays(-(i % 90)), UpdatedAt = now.AddMinutes(-(i % 1440)), });
        }

        _db.SaveChanges();
    }

    [GlobalCleanup]
    public void Cleanup() => _db.Dispose();
    // ---------- BASELINE: solo filtro BusinessId ----------
    [Benchmark(Baseline = true, Description = "Inbox: sin filtro estado, ORDER BY UpdatedAt")]
    public async Task GetConversations_NoStatusFilter()
    {
        var _ = await _db.Conversations.AsNoTracking().Where(c => c.BusinessId == BusinessId).OrderByDescending(c => c.UpdatedAt).Take(100).ToListAsync();
    }

    // ---------- FILTRO BotActive ----------
    [Benchmark(Description = "Inbox: filtro BotActive, ORDER BY UpdatedAt")]
    public async Task GetConversations_StatusBotActive()
    {
        var _ = await _db.Conversations.AsNoTracking().Where(c => c.BusinessId == BusinessId && c.Status == ConversationStatus.BotActive).OrderByDescending(c => c.UpdatedAt).Take(100).ToListAsync();
    }

    // ---------- FILTRO Resolved ----------
    [Benchmark(Description = "Inbox: filtro Resolved, ORDER BY UpdatedAt")]
    public async Task GetConversations_StatusResolved()
    {
        var _ = await _db.Conversations.AsNoTracking().Where(c => c.BusinessId == BusinessId && c.Status == ConversationStatus.Resolved).OrderByDescending(c => c.UpdatedAt).Take(100).ToListAsync();
    }
}