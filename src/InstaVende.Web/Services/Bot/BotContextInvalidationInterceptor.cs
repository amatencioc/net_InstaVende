using InstaVende.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace InstaVende.Web.Services.Bot;

/// <summary>
/// Invalida la caché del <see cref="BotContext"/> cuando se guardan cambios en cualquier
/// entidad que alimenta el prompt del vendedor. Así ningún controlador necesita recordar
/// invalidarla y el bot nunca responde con configuración obsoleta.
/// </summary>
public sealed class BotContextInvalidationInterceptor : SaveChangesInterceptor
{
    private readonly IMemoryCache _cache;

    // Negocios afectados por el SaveChanges en curso (el interceptor es singleton: se indexa por contexto)
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbContext, HashSet<int>> _pending = new();

    public BotContextInvalidationInterceptor(IMemoryCache cache) => _cache = cache;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null) await CollectAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context != null) CollectAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush(eventData.Context);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private async Task CollectAsync(DbContext context, CancellationToken ct)
    {
        var businessIds = new HashSet<int>();
        var botConfigIds = new HashSet<int>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;
            switch (entry.Entity)
            {
                case Business b:         businessIds.Add(b.Id); break;
                case BotConfig c:        businessIds.Add(c.BusinessId); break;
                case VendorConfig v:     businessIds.Add(v.BusinessId); break;
                case KnowledgeEntry k:   businessIds.Add(k.BusinessId); break;
                case PaymentMethod p:    businessIds.Add(p.BusinessId); break;
                case DeliveryZone z:     businessIds.Add(z.BusinessId); break;
                case BotIntent i:        botConfigIds.Add(i.BotConfigId); break;
                case BotKnowledge kb:    botConfigIds.Add(kb.BotConfigId); break;
            }
        }

        if (botConfigIds.Count > 0)
        {
            var ids = await context.Set<BotConfig>().AsNoTracking()
                .Where(c => botConfigIds.Contains(c.Id))
                .Select(c => c.BusinessId)
                .ToListAsync(ct);
            businessIds.UnionWith(ids);
        }

        businessIds.Remove(0);
        if (businessIds.Count == 0) return;

        var pending = _pending.GetValue(context, _ => []);
        lock (pending) pending.UnionWith(businessIds);
    }

    private void Flush(DbContext? context)
    {
        if (context == null || !_pending.TryGetValue(context, out var pending)) return;
        int[] ids;
        lock (pending) { ids = pending.ToArray(); pending.Clear(); }
        foreach (var id in ids) BotContextProvider.Invalidate(_cache, id);
    }

    private void Discard(DbContext? context)
    {
        if (context != null && _pending.TryGetValue(context, out var pending))
            lock (pending) pending.Clear();
    }
}
