using InstaVende.Core.Domain;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Analytics;

/// <summary>Página de inicio: KPIs del día y de la semana, actividad reciente y configuración inicial.</summary>
[Route("api/dashboard")]
public sealed class DashboardController(AppDbContext db) : TenantController
{
    private const int SeriesDays = 14;
    private const int ListSize = 5;

    /// <summary>
    /// Resumen del negocio. Seis consultas agregadas y secuenciales (mismo DbContext): pedidos, conversaciones
    /// y mensajes por hora de las últimas dos semanas, estado operativo + checklist, últimos pedidos y
    /// conversaciones que requieren atención.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct)
    {
        var tz = Tenant.TimeZone;
        var now = DateTime.UtcNow;
        var end = AnalyticsTime.FloorHour(now).AddHours(1); // fin exclusivo: incluye la hora en curso
        var today = AnalyticsTime.LocalDay(now, tz);
        var todayStart = AnalyticsTime.StartOfDayUtc(today, tz);
        var yesterdayStart = AnalyticsTime.StartOfDayUtc(today.AddDays(-1), tz);
        var seriesFrom = today.AddDays(-(SeriesDays - 1));
        var seriesStart = AnalyticsTime.StartOfDayUtc(seriesFrom, tz);
        var last7Start = end.AddDays(-7);
        var previous7Start = end.AddDays(-14);
        var origin = AnalyticsTime.FloorHour(seriesStart < previous7Start ? seriesStart : previous7Start);

        var orders = await AnalyticsQueries.OrderBuckets(db, BusinessId, origin, end).ToListAsync(ct);
        var conversations = await AnalyticsQueries.ConversationBuckets(db, BusinessId, origin, end).ToListAsync(ct);
        var messages = await AnalyticsQueries.MessageBuckets(db, BusinessId, origin, end).ToListAsync(ct);
        var snapshot = await AnalyticsQueries.DashboardSnapshot(db, BusinessId).FirstOrDefaultAsync(ct)
                       ?? new DashboardSnapshotRow(0, 0, false, 0, false, false, false);
        var recentOrders = await AnalyticsQueries.RecentOrders(db, BusinessId, ListSize).ToListAsync(ct);
        var attention = await AnalyticsQueries.AttentionConversations(db, BusinessId, ListSize).ToListAsync(ct);

        DateTime Start(int hour) => AnalyticsTime.BucketStart(origin, hour);

        DashboardKpisDto Kpis(DateTime fromUtc, DateTime toUtc)
        {
            bool In(int hour) { var s = Start(hour); return s >= fromUtc && s < toUtc; }
            var o = orders.Where(b => In(b.Hour)).ToList();
            var revenue = o.Sum(b => b.Revenue);
            return new DashboardKpisDto(
                NewConversations: conversations.Where(b => In(b.Hour)).Sum(b => b.Conversations),
                InboundMessages: messages.Where(b => In(b.Hour)).Sum(b => b.Inbound),
                Orders: o.Sum(b => b.Orders),
                Revenue: revenue,
                AverageTicket: AnalyticsTime.Average(revenue, o.Sum(b => b.RevenueOrders)));
        }

        // Serie diaria por día local del negocio
        var daily = AnalyticsTime.Days(seriesFrom, today).ToDictionary(d => d, _ => (Conversations: 0, Orders: 0, Revenue: 0m));
        foreach (var b in conversations)
        {
            var day = AnalyticsTime.LocalDay(Start(b.Hour), tz);
            if (daily.TryGetValue(day, out var acc)) daily[day] = acc with { Conversations = acc.Conversations + b.Conversations };
        }
        foreach (var b in orders)
        {
            var day = AnalyticsTime.LocalDay(Start(b.Hour), tz);
            if (daily.TryGetValue(day, out var acc)) daily[day] = acc with { Orders = acc.Orders + b.Orders, Revenue = acc.Revenue + b.Revenue };
        }

        // Distribución por canal (mismo rango que la serie)
        var channels = Enum.GetValues<ChannelType>()
            .Select(ch => new DashboardChannelDto(
                ch,
                conversations.Where(b => b.Channel == ch && Start(b.Hour) >= seriesStart).Sum(b => b.Conversations),
                orders.Where(b => b.Channel == ch && Start(b.Hour) >= seriesStart).Sum(b => b.Orders),
                orders.Where(b => b.Channel == ch && Start(b.Hour) >= seriesStart).Sum(b => b.Revenue)))
            .Where(c => c.Conversations > 0 || c.Orders > 0)
            .OrderByDescending(c => c.Conversations).ThenByDescending(c => c.Revenue)
            .ToList();

        return new DashboardDto(
            GeneratedAt: now,
            LocalDate: today,
            Today: Kpis(todayStart, end),
            Yesterday: Kpis(yesterdayStart, end.AddDays(-1)),
            Last7Days: Kpis(last7Start, end),
            Previous7Days: Kpis(previous7Start, last7Start),
            ConversationsWaitingHuman: snapshot.ConversationsWaitingHuman,
            OrdersInValidation: snapshot.OrdersInValidation,
            Daily: daily.OrderBy(kv => kv.Key)
                .Select(kv => new DashboardDailyPointDto(kv.Key, kv.Value.Conversations, kv.Value.Orders, kv.Value.Revenue))
                .ToList(),
            Channels: channels,
            RecentOrders: recentOrders.Select(o => new DashboardRecentOrderDto(
                    o.Id, o.OrderNumber, DisplayName(o.CustomerName), o.Total, o.SubStatus,
                    OrderWorkflow.Label(o.SubStatus), OrderWorkflow.BadgeCss(o.SubStatus), o.Channel, o.CreatedAt))
                .ToList(),
            AttentionConversations: attention.Select(c => new DashboardAttentionConversationDto(
                    c.Id, c.ContactId, DisplayName(c.ContactName), c.Channel, c.Status, c.LastMessagePreview,
                    c.LastMessageAt, c.UnreadCount))
                .ToList(),
            Setup: BuildSetup(snapshot));
    }

    private static string DisplayName(string? name) => string.IsNullOrWhiteSpace(name) ? "Cliente sin nombre" : name.Trim();

    /// <summary>Checklist "Primeros pasos" (siempre calculado sobre los datos reales).</summary>
    private static DashboardSetupDto BuildSetup(DashboardSnapshotRow s)
    {
        DashboardSetupStepDto[] steps =
        [
            new("channel", "Conecta un canal",
                "Vincula WhatsApp, Instagram o Messenger para empezar a recibir mensajes.", s.ChannelConnected),
            new("products", "Agrega tus productos",
                "Tu vendedor solo puede ofrecer lo que está en tu catálogo.", s.ActiveProducts > 0),
            new("payments", "Configura tus métodos de pago",
                "Indica cómo te pagan tus clientes: transferencia, billetera digital o link de pago.", s.PaymentMethodActive),
            new("vendor", "Personaliza a tu vendedor",
                "Ponle nombre y escribe su mensaje de bienvenida.", s.VendorConfigured),
            new("bot-test", "Prueba a tu vendedor",
                "Conversa con tu vendedor IA antes de atender a clientes reales.", s.BotTested),
        ];
        var done = steps.Count(st => st.Done);
        return new DashboardSetupDto(done, steps.Length, (int)Math.Round(done * 100.0 / steps.Length), steps);
    }
}
