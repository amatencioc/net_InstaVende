using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Domain;
using InstaVende.Core.Enums;

namespace InstaVende.Web.Features.Orders;

// ── Flujo (metadatos de presentación derivados de OrderWorkflow) ─────────────

/// <summary>Pestañas del tablero con sus columnas (sub-estados) en orden.</summary>
public sealed record OrderWorkflowDto(IReadOnlyList<OrderWorkflowTabDto> Tabs);

public sealed record OrderWorkflowTabDto(OrderBoardTab Tab, string Label, IReadOnlyList<OrderSubStatusInfoDto> Columns);

/// <summary>Sub-estado con su etiqueta, clase de color (<c>st-*</c>), pestaña y estado agregado.</summary>
public sealed record OrderSubStatusInfoDto(
    OrderSubStatus SubStatus,
    string Label,
    string Css,
    OrderBoardTab Tab,
    OrderStatus Status,
    bool CountsAsRevenue);

// ── Tablero ──────────────────────────────────────────────────────────────────

/// <summary>Tablero kanban de una pestaña: columnas con sus tarjetas y contadores de todas las pestañas.</summary>
public sealed record OrderBoardDto(
    OrderBoardTab Tab,
    IReadOnlyList<OrderBoardColumnDto> Columns,
    IReadOnlyList<OrderBoardTabCountDto> TabCounts);

public sealed record OrderBoardTabCountDto(OrderBoardTab Tab, string Label, int Count);

/// <summary>Columna del tablero. <c>Count</c> es el total real; <c>Orders</c> puede venir limitado.</summary>
public sealed record OrderBoardColumnDto(
    OrderSubStatus SubStatus,
    string Label,
    string Css,
    int Count,
    decimal TotalAmount,
    IReadOnlyList<OrderCardDto> Orders);

/// <summary>Tarjeta compacta de un pedido en el tablero.</summary>
public sealed record OrderCardDto(
    int Id,
    string OrderNumber,
    int ContactId,
    string ContactName,
    ChannelType Channel,
    decimal Total,
    int ItemCount,
    DateTime CreatedAt,
    OrderSubStatus SubStatus,
    string SubStatusLabel,
    string SubStatusCss,
    int? ConversationId);

// ── Detalle ──────────────────────────────────────────────────────────────────

public sealed record OrderDetailDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    string StatusLabel,
    OrderSubStatus SubStatus,
    string SubStatusLabel,
    string SubStatusCss,
    OrderBoardTab Tab,
    ChannelType Channel,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingCost,
    decimal Total,
    string? PaymentMethodName,
    string? ShippingAddress,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? ConversationId,
    OrderContactDto Contact,
    IReadOnlyList<OrderLineDto> Items);

public sealed record OrderContactDto(int Id, string Name, string? Phone, ChannelType Channel, string? AvatarUrl);

public sealed record OrderLineDto(
    int Id,
    int ProductId,
    string ProductName,
    string? VariantDescription,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

// ── Comandos ─────────────────────────────────────────────────────────────────

/// <summary>Mover un pedido a otro sub-estado (columna).</summary>
public sealed record OrderMoveRequest(
    [Required(ErrorMessage = "Indica el estado de destino.")] OrderSubStatus SubStatus,
    bool NotifyCustomer = false);

public sealed record OrderMoveResultDto(
    int Id,
    OrderStatus Status,
    OrderSubStatus SubStatus,
    string SubStatusLabel,
    string SubStatusCss,
    OrderBoardTab Tab,
    DateTime UpdatedAt,
    bool CustomerNotified,
    string? NotificationError);

/// <summary>Datos administrativos editables. Los valores vacíos se guardan como "sin dato".</summary>
public sealed record OrderUpdateRequest(
    [StringLength(1000, ErrorMessage = "Las notas no pueden superar 1000 caracteres.")] string? Notes,
    [StringLength(500, ErrorMessage = "La dirección no puede superar 500 caracteres.")] string? ShippingAddress,
    [StringLength(150, ErrorMessage = "El método de pago no puede superar 150 caracteres.")] string? PaymentMethodName);

internal static class OrderPresentation
{
    public static string TabLabel(OrderBoardTab tab) => tab switch
    {
        OrderBoardTab.Nuevos      => "Nuevos",
        OrderBoardTab.EnCurso     => "En curso",
        OrderBoardTab.Completados => "Completados",
        OrderBoardTab.Cancelados  => "Cancelados",
        _ => tab.ToString()
    };

    public static readonly OrderBoardTab[] Tabs =
        [OrderBoardTab.Nuevos, OrderBoardTab.EnCurso, OrderBoardTab.Completados, OrderBoardTab.Cancelados];
}
