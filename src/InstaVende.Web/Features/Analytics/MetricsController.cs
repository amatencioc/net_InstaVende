using InstaVende.Core.Domain;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Analytics;

/// <summary>Métricas de ventas, embudo y rendimiento del bot por periodo, con comparativa.</summary>
[Route("api/metrics")]
public sealed class MetricsController(AppDbContext db) : TenantController
{
    private const int MaxCustomDays = 366;
    private const int TopSize = 10;

    /// <summary>
    /// Métricas del periodo y del periodo anterior de igual duración. Los periodos son días locales
    /// del negocio con ambos extremos incluidos: <c>7d</c>, <c>30d</c> y <c>90d</c> terminan hoy;
    /// <c>custom</c> exige <c>from</c> y <c>to</c> (yyyy-MM-dd, máximo 366 días, sin fechas futuras).
    /// </summary>
    /// <param name="period">7d | 30d | 90d | custom (por defecto 7d).</param>
    /// <param name="from">Inicio del periodo personalizado (fecha local).</param>
    /// <param name="to">Fin del periodo personalizado (fecha local, incluida).</param>
    [HttpGet]
    [ProducesResponseType<MetricsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MetricsDto>> Get(
        [FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var tz = Tenant.TimeZone;
        var now = DateTime.UtcNow;
        var today = AnalyticsTime.LocalDay(now, tz);

        var key = string.IsNullOrWhiteSpace(period) ? "7d" : period.Trim().ToLowerInvariant();
        DateOnly rangeFrom, rangeTo;
        switch (key)
        {
            case "7d" or "30d" or "90d":
                rangeTo = today;
                rangeFrom = today.AddDays(-(int.Parse(key[..^1]) - 1));
                break;
            case "custom":
                if (from is null || to is null)
                    return BadRequestProblem("Indica las fechas de inicio y fin del periodo personalizado.");
                if (from > to)
                    return BadRequestProblem("La fecha de inicio no puede ser posterior a la fecha de fin.");
                if (to > today)
                    return BadRequestProblem("El periodo no puede terminar después de hoy.");
                if (to.Value.DayNumber - from.Value.DayNumber + 1 > MaxCustomDays)
                    return BadRequestProblem($"El periodo personalizado no puede superar {MaxCustomDays} días.");
                rangeFrom = from.Value;
                rangeTo = to.Value;
                break;
            default:
                return BadRequestProblem("Periodo no válido. Usa 7d, 30d, 90d o custom.");
        }

        var days = rangeTo.DayNumber - rangeFrom.DayNumber + 1;
        var previousTo = rangeFrom.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(days - 1));

        var start = AnalyticsTime.StartOfDayUtc(rangeFrom, tz);
        var end = AnalyticsTime.StartOfDayUtc(rangeTo.AddDays(1), tz);
        var previousStart = AnalyticsTime.StartOfDayUtc(previousFrom, tz);
        var origin = AnalyticsTime.FloorHour(previousStart);
        var currentFromBucket = AnalyticsTime.FloorHour(start);

        // Consultas agregadas y secuenciales (un DbContext no admite operaciones concurrentes)
        var orders = await AnalyticsQueries.OrderBuckets(db, BusinessId, origin, end).ToListAsync(ct);
        var conversations = await AnalyticsQueries.ConversationBuckets(db, BusinessId, origin, end).ToListAsync(ct);
        var messages = await AnalyticsQueries.MessageBuckets(db, BusinessId, origin, end).ToListAsync(ct);
        var counts = await AnalyticsQueries.MetricsCounts(db, BusinessId, previousStart, start, end).FirstOrDefaultAsync(ct)
                     ?? new MetricsCountsRow(0, 0, 0, 0, 0, 0, 0, 0);
        var firstResponse = await AnalyticsQueries.AverageFirstResponseSeconds(db, BusinessId, start, end).AverageAsync(ct);
        var previousFirstResponse = await AnalyticsQueries.AverageFirstResponseSeconds(db, BusinessId, previousStart, start).AverageAsync(ct);
        var topByUnits = await AnalyticsQueries.TopProducts(db, BusinessId, start, end, byRevenue: false, TopSize).ToListAsync(ct);
        var topByRevenue = await AnalyticsQueries.TopProducts(db, BusinessId, start, end, byRevenue: true, TopSize).ToListAsync(ct);
        var topCustomers = await AnalyticsQueries.TopCustomers(db, BusinessId, start, end, TopSize).ToListAsync(ct);
        var subStatuses = await AnalyticsQueries.SubStatusDistribution(db, BusinessId, start, end).ToListAsync(ct);

        DateTime Start(int hour) => AnalyticsTime.BucketStart(origin, hour);
        bool IsCurrent(int hour) => Start(hour) >= currentFromBucket;

        var curOrders = orders.Where(b => IsCurrent(b.Hour)).ToList();
        var prevOrders = orders.Where(b => !IsCurrent(b.Hour)).ToList();
        var curConversations = conversations.Where(b => IsCurrent(b.Hour)).ToList();
        var prevConversations = conversations.Where(b => !IsCurrent(b.Hour)).ToList();
        var curMessages = messages.Where(b => IsCurrent(b.Hour)).ToList();
        var prevMessages = messages.Where(b => !IsCurrent(b.Hour)).ToList();

        var sales = Sales(curOrders, curConversations.Sum(b => b.Conversations), counts.ConversationsWithOrder, counts.NewContacts);
        var previousSales = Sales(prevOrders, prevConversations.Sum(b => b.Conversations), counts.PreviousConversationsWithOrder, counts.PreviousNewContacts);
        var bot = Bot(curMessages, sales.Conversations, counts.Handoffs, firstResponse);
        var previousBot = Bot(prevMessages, previousSales.Conversations, counts.PreviousHandoffs, previousFirstResponse);

        MetricsFunnelStepDto[] funnel =
        [
            new("contacts", "Contactos nuevos", counts.NewContacts),
            new("conversations", "Conversaciones", sales.Conversations),
            new("carts", "Con carrito", counts.ConversationsWithCart),
            new("orders", "Con pedido", counts.ConversationsWithOrder),
            new("completed", "Pedido entregado", counts.ConversationsCompleted),
        ];

        // Serie diaria por día local
        var daily = AnalyticsTime.Days(rangeFrom, rangeTo).ToDictionary(d => d, d => new MetricsDailyPointDto(d, 0m, 0, 0, 0, 0));
        foreach (var b in curOrders)
            Update(Start(b.Hour), p => p with { Revenue = p.Revenue + b.Revenue, Orders = p.Orders + b.Orders });
        foreach (var b in curConversations)
            Update(Start(b.Hour), p => p with { Conversations = p.Conversations + b.Conversations });
        foreach (var b in curMessages)
            Update(Start(b.Hour), p => p with { BotMessages = p.BotMessages + b.BotOutbound, HumanMessages = p.HumanMessages + b.HumanOutbound });

        void Update(DateTime bucketStart, Func<MetricsDailyPointDto, MetricsDailyPointDto> change)
        {
            var day = AnalyticsTime.LocalDay(bucketStart, tz);
            if (daily.TryGetValue(day, out var point)) daily[day] = change(point);
        }

        var channels = Enum.GetValues<ChannelType>()
            .Select(ch => new MetricsChannelDto(
                ch,
                curOrders.Where(b => b.Channel == ch).Sum(b => b.Revenue),
                curOrders.Where(b => b.Channel == ch).Sum(b => b.Orders),
                curConversations.Where(b => b.Channel == ch).Sum(b => b.Conversations)))
            .Where(c => c.Orders > 0 || c.Conversations > 0)
            .OrderByDescending(c => c.Revenue).ThenByDescending(c => c.Conversations)
            .ToList();

        var subStatusByKey = subStatuses.ToDictionary(s => s.SubStatus);
        var distribution = Enum.GetValues<OrderSubStatus>()
            .Select(s => subStatusByKey.TryGetValue(s, out var row)
                ? new MetricsSubStatusDto(s, OrderWorkflow.Label(s), OrderWorkflow.BadgeCss(s), row.Orders, row.Total)
                : new MetricsSubStatusDto(s, OrderWorkflow.Label(s), OrderWorkflow.BadgeCss(s), 0, 0m))
            .ToList();

        return new MetricsDto(
            Range: new MetricsRangeDto(key, rangeFrom, rangeTo, previousFrom, previousTo, days, now),
            Sales: sales,
            PreviousSales: previousSales,
            Bot: bot,
            PreviousBot: previousBot,
            Funnel: funnel,
            Daily: daily.Values.OrderBy(p => p.Date).ToList(),
            Channels: channels,
            TopProductsByUnits: topByUnits.Select(ToDto).ToList(),
            TopProductsByRevenue: topByRevenue.Select(ToDto).ToList(),
            TopCustomers: topCustomers
                .OrderByDescending(c => c.Spent).ThenBy(c => c.ContactId)
                .Select(c => new MetricsTopCustomerDto(
                    c.ContactId, string.IsNullOrWhiteSpace(c.Name) ? "Cliente sin nombre" : c.Name.Trim(),
                    c.Channel, c.Orders, c.Spent, c.LastOrderAt))
                .ToList(),
            SubStatuses: distribution);
    }

    private static MetricsSalesDto Sales(List<AnalyticsOrderBucket> orders, int conversations, int conversationsWithOrder, int newContacts)
    {
        var revenue = orders.Sum(b => b.Revenue);
        var revenueOrders = orders.Sum(b => b.RevenueOrders);
        return new MetricsSalesDto(
            Revenue: revenue,
            Orders: orders.Sum(b => b.Orders),
            RevenueOrders: revenueOrders,
            AverageTicket: AnalyticsTime.Average(revenue, revenueOrders),
            Conversations: conversations,
            ConversationsWithOrder: conversationsWithOrder,
            ConversionRate: AnalyticsTime.Percent(conversationsWithOrder, conversations),
            NewContacts: newContacts);
    }

    private static MetricsBotDto Bot(List<AnalyticsMessageBucket> messages, int conversations, int handoffs, double? firstResponseSeconds)
    {
        var bot = messages.Sum(b => b.BotOutbound);
        var human = messages.Sum(b => b.HumanOutbound);
        return new MetricsBotDto(
            BotMessages: bot,
            HumanMessages: human,
            AutomationRate: AnalyticsTime.Percent(bot, bot + human),
            Conversations: conversations,
            Handoffs: handoffs,
            HandoffRate: AnalyticsTime.Percent(handoffs, conversations),
            AverageFirstResponseSeconds: firstResponseSeconds is { } s ? Math.Round(s, 0) : null);
    }

    private static MetricsTopProductDto ToDto(MetricsTopProductRow p)
        => new(p.ProductId, string.IsNullOrWhiteSpace(p.Name) ? $"Producto #{p.ProductId}" : p.Name, p.Units, p.Revenue, p.Orders);
}
