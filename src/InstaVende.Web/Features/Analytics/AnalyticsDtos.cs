using InstaVende.Core.Enums;

namespace InstaVende.Web.Features.Analytics;

// ── Inicio (GET /api/dashboard) ──────────────────────────────────────────────

/// <summary>Resumen de la página de inicio del panel.</summary>
/// <param name="GeneratedAt">Instante (UTC) en que se calculó.</param>
/// <param name="LocalDate">Fecha de hoy en la zona horaria del negocio.</param>
/// <param name="Today">Desde las 00:00 locales hasta ahora.</param>
/// <param name="Yesterday">Ayer hasta la misma hora que ahora (comparativa justa con <paramref name="Today"/>).</param>
/// <param name="Last7Days">Últimas 7×24 horas.</param>
/// <param name="Previous7Days">Las 7×24 horas anteriores.</param>
/// <param name="ConversationsWaitingHuman">Conversaciones en estado "esperando humano" ahora mismo.</param>
/// <param name="OrdersInValidation">Pedidos en el sub-estado "En validación" ahora mismo.</param>
/// <param name="Daily">Serie de los últimos 14 días locales (incluye hoy).</param>
/// <param name="Channels">Conversaciones, pedidos e ingresos por canal en los últimos 14 días.</param>
public sealed record DashboardDto(
    DateTime GeneratedAt,
    DateOnly LocalDate,
    DashboardKpisDto Today,
    DashboardKpisDto Yesterday,
    DashboardKpisDto Last7Days,
    DashboardKpisDto Previous7Days,
    int ConversationsWaitingHuman,
    int OrdersInValidation,
    IReadOnlyList<DashboardDailyPointDto> Daily,
    IReadOnlyList<DashboardChannelDto> Channels,
    IReadOnlyList<DashboardRecentOrderDto> RecentOrders,
    IReadOnlyList<DashboardAttentionConversationDto> AttentionConversations,
    DashboardSetupDto Setup);

/// <summary>Indicadores de una ventana de tiempo. Ingresos según <c>OrderWorkflow.RevenueStatuses</c>.</summary>
/// <param name="Orders">Pedidos reales (excluye carritos; incluye cancelados).</param>
/// <param name="AverageTicket">Ingresos / pedidos que suman ingresos.</param>
public sealed record DashboardKpisDto(int NewConversations, int InboundMessages, int Orders, decimal Revenue, decimal AverageTicket);

public sealed record DashboardDailyPointDto(DateOnly Date, int Conversations, int Orders, decimal Revenue);

public sealed record DashboardChannelDto(ChannelType Channel, int Conversations, int Orders, decimal Revenue);

/// <param name="SubStatusCss">Clase de badge de <c>OrderWorkflow.BadgeCss</c> (st-green, st-red…).</param>
public sealed record DashboardRecentOrderDto(
    int Id, string OrderNumber, string CustomerName, decimal Total, OrderSubStatus SubStatus,
    string SubStatusLabel, string SubStatusCss, ChannelType Channel, DateTime CreatedAt);

public sealed record DashboardAttentionConversationDto(
    int Id, int ContactId, string ContactName, ChannelType Channel, ConversationStatus Status,
    string? LastMessagePreview, DateTime? LastMessageAt, int UnreadCount);

/// <summary>Checklist de configuración inicial calculado en vivo.</summary>
/// <param name="Percent">0–100.</param>
public sealed record DashboardSetupDto(int CompletedSteps, int TotalSteps, int Percent, IReadOnlyList<DashboardSetupStepDto> Steps);

/// <param name="Key">channel | products | payments | vendor | bot-test</param>
public sealed record DashboardSetupStepDto(string Key, string Title, string Description, bool Done);

// ── Métricas (GET /api/metrics) ──────────────────────────────────────────────

/// <summary>Métricas de un periodo y del periodo anterior de igual duración.</summary>
/// <param name="Daily">Serie diaria (días locales) del periodo actual.</param>
/// <param name="Funnel">Embudo del periodo actual sobre conversaciones creadas en él.</param>
public sealed record MetricsDto(
    MetricsRangeDto Range,
    MetricsSalesDto Sales,
    MetricsSalesDto PreviousSales,
    MetricsBotDto Bot,
    MetricsBotDto PreviousBot,
    IReadOnlyList<MetricsFunnelStepDto> Funnel,
    IReadOnlyList<MetricsDailyPointDto> Daily,
    IReadOnlyList<MetricsChannelDto> Channels,
    IReadOnlyList<MetricsTopProductDto> TopProductsByUnits,
    IReadOnlyList<MetricsTopProductDto> TopProductsByRevenue,
    IReadOnlyList<MetricsTopCustomerDto> TopCustomers,
    IReadOnlyList<MetricsSubStatusDto> SubStatuses);

/// <summary>Rango resuelto (fechas locales del negocio, ambos extremos incluidos).</summary>
/// <param name="Period">7d | 30d | 90d | custom</param>
public sealed record MetricsRangeDto(
    string Period, DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo, int Days, DateTime GeneratedAt);

/// <param name="Orders">Pedidos reales (excluye carritos; incluye cancelados).</param>
/// <param name="RevenueOrders">Pedidos que suman ingresos (<c>OrderWorkflow.RevenueStatuses</c>).</param>
/// <param name="AverageTicket">Ingresos / pedidos con ingreso.</param>
/// <param name="ConversionRate">Conversaciones con pedido / conversaciones (0–100).</param>
public sealed record MetricsSalesDto(
    decimal Revenue, int Orders, int RevenueOrders, decimal AverageTicket,
    int Conversations, int ConversationsWithOrder, double ConversionRate, int NewContacts);

/// <param name="BotMessages">Mensajes salientes enviados automáticamente (bot y recordatorios).</param>
/// <param name="HumanMessages">Mensajes salientes escritos por una persona del equipo.</param>
/// <param name="AutomationRate">% de mensajes salientes enviados por el bot (0–100).</param>
/// <param name="Handoffs">
/// Conversaciones del periodo atendidas por una persona. Aproximación: estado actual "esperando humano" o
/// "humano activo", o con al menos un mensaje saliente humano (no hay historial de estados).
/// </param>
/// <param name="HandoffRate">Derivaciones / conversaciones (0–100).</param>
/// <param name="AverageFirstResponseSeconds">Primer entrante → primer saliente; null si no hay datos.</param>
public sealed record MetricsBotDto(
    int BotMessages, int HumanMessages, double AutomationRate, int Conversations,
    int Handoffs, double HandoffRate, double? AverageFirstResponseSeconds);

/// <param name="Key">contacts | conversations | carts | orders | completed</param>
public sealed record MetricsFunnelStepDto(string Key, string Label, int Value);

public sealed record MetricsDailyPointDto(
    DateOnly Date, decimal Revenue, int Orders, int Conversations, int BotMessages, int HumanMessages);

public sealed record MetricsChannelDto(ChannelType Channel, decimal Revenue, int Orders, int Conversations);

public sealed record MetricsTopProductDto(int ProductId, string Name, int Units, decimal Revenue, int Orders);

public sealed record MetricsTopCustomerDto(
    int ContactId, string Name, ChannelType Channel, int Orders, decimal Spent, DateTime LastOrderAt);

public sealed record MetricsSubStatusDto(OrderSubStatus SubStatus, string Label, string Css, int Orders, decimal Total);
