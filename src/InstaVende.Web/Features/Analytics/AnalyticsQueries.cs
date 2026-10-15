using InstaVende.Core.Domain;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Analytics;

// ── Filas internas (no forman parte del contrato de la API) ──────────────────

/// <summary>Pedidos reales (sin carritos) agregados por hora UTC y canal.</summary>
internal sealed record AnalyticsOrderBucket(int Hour, ChannelType Channel, int Orders, int RevenueOrders, decimal Revenue);

/// <summary>Conversaciones nuevas agregadas por hora UTC y canal.</summary>
internal sealed record AnalyticsConversationBucket(int Hour, ChannelType Channel, int Conversations);

/// <summary>Mensajes agregados por hora UTC: entrantes, salientes del bot y salientes de un humano.</summary>
internal sealed record AnalyticsMessageBucket(int Hour, int Inbound, int BotOutbound, int HumanOutbound);

internal sealed record DashboardSnapshotRow(
    int ConversationsWaitingHuman, int OrdersInValidation, bool ChannelConnected, int ActiveProducts,
    bool PaymentMethodActive, bool VendorConfigured, bool BotTested);

internal sealed record DashboardRecentOrderRow(
    int Id, string OrderNumber, string? CustomerName, decimal Total, OrderSubStatus SubStatus, ChannelType Channel, DateTime CreatedAt);

internal sealed record DashboardAttentionRow(
    int Id, int ContactId, string? ContactName, ChannelType Channel, ConversationStatus Status,
    string? LastMessagePreview, DateTime? LastMessageAt, int UnreadCount);

internal sealed record MetricsCountsRow(
    int NewContacts, int PreviousNewContacts,
    int ConversationsWithCart,
    int ConversationsWithOrder, int PreviousConversationsWithOrder,
    int ConversationsCompleted,
    int Handoffs, int PreviousHandoffs);

internal sealed record MetricsFirstResponseRow(double? CurrentSeconds, double? PreviousSeconds);

internal sealed record MetricsTopProductRow(int ProductId, string? Name, int Units, decimal Revenue, int Orders);

internal sealed record MetricsTopCustomerRow(int ContactId, string? Name, ChannelType Channel, int Orders, decimal Spent, DateTime LastOrderAt);

internal sealed record MetricsSubStatusRow(OrderSubStatus SubStatus, int Orders, decimal Total);

/// <summary>
/// Consultas de analítica. Todas filtran por negocio, son de solo lectura (<c>AsNoTracking</c>) y
/// agregan en SQL: nunca materializan filas individuales de pedidos, mensajes o conversaciones.
/// Se exponen como <see cref="IQueryable{T}"/> para poder inspeccionar su SQL (<c>ToQueryString()</c>).
/// </summary>
internal static class AnalyticsQueries
{
    /// <summary>
    /// Pedidos reales (<see cref="OrderWorkflow.OrderStatuses"/>, nunca carritos) creados en
    /// [<paramref name="originUtc"/>, <paramref name="endUtc"/>), agrupados por hora desde el origen y canal.
    /// Los ingresos solo suman <see cref="OrderWorkflow.RevenueStatuses"/>.
    /// </summary>
    public static IQueryable<AnalyticsOrderBucket> OrderBuckets(AppDbContext db, int businessId, DateTime originUtc, DateTime endUtc)
    {
        var orderStatuses = OrderWorkflow.OrderStatuses;
        var revenueStatuses = OrderWorkflow.RevenueStatuses;
        return db.Orders.AsNoTracking()
            .Where(o => o.BusinessId == businessId && o.CreatedAt >= originUtc && o.CreatedAt < endUtc
                        && orderStatuses.Contains(o.Status))
            .GroupBy(o => new { Hour = EF.Functions.DateDiffHour(originUtc, o.CreatedAt), o.ChannelType })
            .Select(g => new AnalyticsOrderBucket(
                g.Key.Hour,
                g.Key.ChannelType,
                g.Count(),
                g.Count(o => revenueStatuses.Contains(o.Status)),
                g.Sum(o => revenueStatuses.Contains(o.Status) ? o.Total : 0m)));
    }

    /// <summary>Conversaciones creadas en [origen, fin) agrupadas por hora y canal.</summary>
    public static IQueryable<AnalyticsConversationBucket> ConversationBuckets(AppDbContext db, int businessId, DateTime originUtc, DateTime endUtc)
        => db.Conversations.AsNoTracking()
            .Where(c => c.BusinessId == businessId && c.CreatedAt >= originUtc && c.CreatedAt < endUtc)
            .GroupBy(c => new { Hour = EF.Functions.DateDiffHour(originUtc, c.CreatedAt), c.ChannelType })
            .Select(g => new AnalyticsConversationBucket(g.Key.Hour, g.Key.ChannelType, g.Count()));

    /// <summary>
    /// Mensajes enviados en [origen, fin) agrupados por hora: entrantes, salientes del bot
    /// (<c>SentByBot</c>, incluye recordatorios automáticos) y salientes escritos por una persona.
    /// </summary>
    public static IQueryable<AnalyticsMessageBucket> MessageBuckets(AppDbContext db, int businessId, DateTime originUtc, DateTime endUtc)
        => db.Messages.AsNoTracking()
            .Where(m => m.Conversation.BusinessId == businessId && m.SentAt >= originUtc && m.SentAt < endUtc)
            .GroupBy(m => EF.Functions.DateDiffHour(originUtc, m.SentAt))
            .Select(g => new AnalyticsMessageBucket(
                g.Key,
                g.Count(m => m.Direction == MessageDirection.Inbound),
                g.Count(m => m.Direction == MessageDirection.Outbound && m.SentByBot),
                g.Count(m => m.Direction == MessageDirection.Outbound && !m.SentByBot)));

    /// <summary>
    /// Estado operativo y checklist de configuración inicial en una sola ida a la BD
    /// (subconsultas escalares sobre la fila del negocio).
    /// </summary>
    public static IQueryable<DashboardSnapshotRow> DashboardSnapshot(AppDbContext db, int businessId)
        => db.Businesses.AsNoTracking()
            .Where(b => b.Id == businessId)
            .Select(b => new DashboardSnapshotRow(
                db.Conversations.Count(c => c.BusinessId == businessId && c.Status == ConversationStatus.WaitingHuman),
                db.Orders.Count(o => o.BusinessId == businessId && o.Status != OrderStatus.Cart
                                     && o.SubStatus == OrderSubStatus.EnValidacion),
                db.ChannelConfigs.Any(c => c.BusinessId == businessId && c.IsActive),
                db.Products.Count(p => p.BusinessId == businessId && p.IsActive),
                db.PaymentMethods.Any(p => p.BusinessId == businessId && p.IsActive),
                db.VendorConfigs.Any(v => v.BusinessId == businessId
                                          && !string.IsNullOrWhiteSpace(v.VendorName)
                                          && !string.IsNullOrWhiteSpace(v.WelcomeMessage)),
                db.OnboardingProgresses.Any(o => o.BusinessId == businessId && o.BotTested)));

    /// <summary>Últimos pedidos reales (sin carritos).</summary>
    public static IQueryable<DashboardRecentOrderRow> RecentOrders(AppDbContext db, int businessId, int take)
        => db.Orders.AsNoTracking()
            .Where(o => o.BusinessId == businessId && o.Status != OrderStatus.Cart)
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Take(take)
            .Select(o => new DashboardRecentOrderRow(
                o.Id, o.OrderNumber, o.Contact.Name ?? o.Contact.Phone, o.Total, o.SubStatus, o.ChannelType, o.CreatedAt));

    /// <summary>
    /// Conversaciones que requieren atención humana: esperando a un humano (primero) o atendidas por
    /// un humano con mensajes sin leer.
    /// </summary>
    public static IQueryable<DashboardAttentionRow> AttentionConversations(AppDbContext db, int businessId, int take)
        => db.Conversations.AsNoTracking()
            .Where(c => c.BusinessId == businessId
                        && (c.Status == ConversationStatus.WaitingHuman
                            || (c.Status == ConversationStatus.HumanActive && c.UnreadCount > 0)))
            .OrderBy(c => c.Status == ConversationStatus.WaitingHuman ? 0 : 1)
            .ThenByDescending(c => c.LastMessageAt ?? c.UpdatedAt)
            .Take(take)
            .Select(c => new DashboardAttentionRow(
                c.Id, c.ContactId, c.Contact.Name ?? c.Contact.Phone, c.ChannelType, c.Status,
                c.LastMessagePreview, c.LastMessageAt, c.UnreadCount));

    /// <summary>
    /// Contadores del embudo y del bot para el periodo [start, end) y el anterior [prevStart, start),
    /// en una sola ida a la BD.
    /// <para>
    /// Derivaciones: no existe historial de estados de la conversación, así que se aproximan como
    /// conversaciones creadas en el periodo que hoy están en <see cref="ConversationStatus.WaitingHuman"/> /
    /// <see cref="ConversationStatus.HumanActive"/> o que tienen al menos un mensaje saliente escrito por una
    /// persona (<c>SentByBot = false</c>).
    /// </para>
    /// </summary>
    public static IQueryable<MetricsCountsRow> MetricsCounts(AppDbContext db, int businessId, DateTime prevStart, DateTime start, DateTime end)
        => db.Businesses.AsNoTracking()
            .Where(b => b.Id == businessId)
            .Select(b => new MetricsCountsRow(
                db.Contacts.Count(c => c.BusinessId == businessId && c.FirstSeenAt >= start && c.FirstSeenAt < end),
                db.Contacts.Count(c => c.BusinessId == businessId && c.FirstSeenAt >= prevStart && c.FirstSeenAt < start),
                db.Conversations.Count(c => c.BusinessId == businessId && c.CreatedAt >= start && c.CreatedAt < end
                                            && c.Orders.Any()),
                db.Conversations.Count(c => c.BusinessId == businessId && c.CreatedAt >= start && c.CreatedAt < end
                                            && c.Orders.Any(o => o.Status != OrderStatus.Cart)),
                db.Conversations.Count(c => c.BusinessId == businessId && c.CreatedAt >= prevStart && c.CreatedAt < start
                                            && c.Orders.Any(o => o.Status != OrderStatus.Cart)),
                db.Conversations.Count(c => c.BusinessId == businessId && c.CreatedAt >= start && c.CreatedAt < end
                                            && c.Orders.Any(o => o.Status == OrderStatus.Delivered)),
                db.Conversations.Count(c => c.BusinessId == businessId && c.CreatedAt >= start && c.CreatedAt < end
                                            && (c.Status == ConversationStatus.WaitingHuman || c.Status == ConversationStatus.HumanActive
                                                || c.Messages.Any(m => m.Direction == MessageDirection.Outbound && !m.SentByBot))),
                db.Conversations.Count(c => c.BusinessId == businessId && c.CreatedAt >= prevStart && c.CreatedAt < start
                                            && (c.Status == ConversationStatus.WaitingHuman || c.Status == ConversationStatus.HumanActive
                                                || c.Messages.Any(m => m.Direction == MessageDirection.Outbound && !m.SentByBot)))));

    /// <summary>
    /// Tiempo medio (segundos) de primera respuesta de las conversaciones creadas en cada periodo:
    /// desde el primer mensaje entrante hasta el primer saliente posterior (bot o humano).
    /// Se agrega por conversación en SQL y se promedia sobre ese resultado.
    /// </summary>
    public static IQueryable<double?> AverageFirstResponseSeconds(AppDbContext db, int businessId, DateTime start, DateTime end)
        => db.Messages.AsNoTracking()
            .Where(m => m.Conversation.BusinessId == businessId
                        && m.Conversation.CreatedAt >= start && m.Conversation.CreatedAt < end)
            .GroupBy(m => m.ConversationId)
            .Select(g => new
            {
                FirstIn = g.Where(m => m.Direction == MessageDirection.Inbound).Min(m => (DateTime?)m.SentAt),
                FirstOut = g.Where(m => m.Direction == MessageDirection.Outbound).Min(m => (DateTime?)m.SentAt),
            })
            .Where(x => x.FirstIn != null && x.FirstOut != null && x.FirstOut >= x.FirstIn)
            .Select(x => (double?)EF.Functions.DateDiffSecond(x.FirstIn!.Value, x.FirstOut!.Value));

    /// <summary>Pedidos con ingreso del periodo (base de rankings de productos y clientes).</summary>
    private static IQueryable<Core.Entities.Order> RevenueOrders(AppDbContext db, int businessId, DateTime start, DateTime end)
    {
        var revenueStatuses = OrderWorkflow.RevenueStatuses;
        return db.Orders.AsNoTracking()
            .Where(o => o.BusinessId == businessId && o.CreatedAt >= start && o.CreatedAt < end
                        && revenueStatuses.Contains(o.Status));
    }

    /// <summary>Productos más vendidos del periodo (por unidades o por ingresos).</summary>
    public static IQueryable<MetricsTopProductRow> TopProducts(AppDbContext db, int businessId, DateTime start, DateTime end, bool byRevenue, int take)
    {
        var grouped = RevenueOrders(db, businessId, start, end)
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Name = g.Max(i => i.ProductName),
                Units = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.Subtotal),
                Orders = g.Select(i => i.OrderId).Distinct().Count(),
            });

        var ordered = byRevenue
            ? grouped.OrderByDescending(x => x.Revenue).ThenByDescending(x => x.Units)
            : grouped.OrderByDescending(x => x.Units).ThenByDescending(x => x.Revenue);

        return ordered.ThenBy(x => x.ProductId)
            .Take(take)
            .Select(x => new MetricsTopProductRow(x.ProductId, x.Name, x.Units, x.Revenue, x.Orders));
    }

    /// <summary>Clientes con mayor gasto del periodo.</summary>
    public static IQueryable<MetricsTopCustomerRow> TopCustomers(AppDbContext db, int businessId, DateTime start, DateTime end, int take)
        => RevenueOrders(db, businessId, start, end)
            .GroupBy(o => o.ContactId)
            .Select(g => new { ContactId = g.Key, Orders = g.Count(), Spent = g.Sum(o => o.Total), LastOrderAt = g.Max(o => o.CreatedAt) })
            .OrderByDescending(x => x.Spent).ThenBy(x => x.ContactId)
            .Take(take)
            .Join(db.Contacts, x => x.ContactId, c => c.Id,
                (x, c) => new MetricsTopCustomerRow(x.ContactId, c.Name ?? c.Phone, c.ChannelType, x.Orders, x.Spent, x.LastOrderAt));

    /// <summary>Pedidos reales del periodo por sub-estado (columna del tablero).</summary>
    public static IQueryable<MetricsSubStatusRow> SubStatusDistribution(AppDbContext db, int businessId, DateTime start, DateTime end)
    {
        var orderStatuses = OrderWorkflow.OrderStatuses;
        return db.Orders.AsNoTracking()
            .Where(o => o.BusinessId == businessId && o.CreatedAt >= start && o.CreatedAt < end
                        && orderStatuses.Contains(o.Status))
            .GroupBy(o => o.SubStatus)
            .Select(g => new MetricsSubStatusRow(g.Key, g.Count(), g.Sum(o => o.Total)));
    }
}
