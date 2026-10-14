using InstaVende.Core.Entities;
using InstaVende.Core.Enums;

namespace InstaVende.Core.Domain;

/// <summary>
/// Única fuente de verdad del ciclo de vida de un pedido.
/// <see cref="Order.SubStatus"/> (columna del tablero) es el estado primario y
/// <see cref="Order.Status"/> se deriva de él. Los carritos del bot
/// (<see cref="OrderStatus.Cart"/>) quedan fuera del flujo hasta confirmarse.
/// </summary>
public static class OrderWorkflow
{
    /// <summary>Estados que cuentan como pedido real (excluye carritos).</summary>
    public static readonly OrderStatus[] OrderStatuses =
    [
        OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Preparing,
        OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Cancelled, OrderStatus.Refunded
    ];

    /// <summary>Estados que suman ingresos: pedidos reales no cancelados ni reembolsados.</summary>
    public static readonly OrderStatus[] RevenueStatuses =
    [
        OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Preparing,
        OrderStatus.Shipped, OrderStatus.Delivered
    ];

    /// <summary>Estados "abiertos" que requieren gestión del comercio.</summary>
    public static readonly OrderStatus[] OpenStatuses =
    [
        OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Shipped
    ];

    public static OrderStatus StatusFor(OrderSubStatus subStatus) => subStatus switch
    {
        OrderSubStatus.EnValidacion   => OrderStatus.Pending,
        OrderSubStatus.EnPreparacion  => OrderStatus.Preparing,
        OrderSubStatus.ListoParaEnvio => OrderStatus.Preparing,
        OrderSubStatus.Enviado        => OrderStatus.Shipped,
        OrderSubStatus.Entregado      => OrderStatus.Delivered,
        OrderSubStatus.Finalizado     => OrderStatus.Delivered,
        OrderSubStatus.Rechazado      => OrderStatus.Cancelled,
        OrderSubStatus.Cancelado      => OrderStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(subStatus), subStatus, null)
    };

    /// <summary>Sub-estado equivalente para un estado agregado (compatibilidad con datos antiguos).</summary>
    public static OrderSubStatus SubStatusFor(OrderStatus status) => status switch
    {
        OrderStatus.Pending   => OrderSubStatus.EnValidacion,
        OrderStatus.Confirmed => OrderSubStatus.EnPreparacion,
        OrderStatus.Preparing => OrderSubStatus.EnPreparacion,
        OrderStatus.Shipped   => OrderSubStatus.Enviado,
        OrderStatus.Delivered => OrderSubStatus.Entregado,
        OrderStatus.Cancelled => OrderSubStatus.Cancelado,
        OrderStatus.Refunded  => OrderSubStatus.Cancelado,
        _ => OrderSubStatus.EnValidacion
    };

    public static bool CanMove(Order order, OrderSubStatus target)
        => order.Status != OrderStatus.Cart && Enum.IsDefined(target) && order.SubStatus != target;

    /// <summary>Mueve el pedido a <paramref name="target"/> manteniendo Status coherente.</summary>
    public static void MoveTo(Order order, OrderSubStatus target)
    {
        if (order.Status == OrderStatus.Cart)
            throw new InvalidOperationException("Un carrito debe confirmarse antes de cambiar de estado.");
        order.SubStatus = target;
        order.Status    = StatusFor(target);
        order.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Convierte un carrito del bot en pedido nuevo ("En validación").</summary>
    public static void ConfirmCart(Order order)
    {
        if (order.Status != OrderStatus.Cart)
            throw new InvalidOperationException("Solo se pueden confirmar carritos.");
        order.SubStatus = OrderSubStatus.EnValidacion;
        order.Status    = OrderStatus.Pending;
        order.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Genera un número de pedido legible y prácticamente único (IV-260514-7KQ2MX).</summary>
    public static string NewOrderNumber()
    {
        const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        Span<char> code = stackalloc char[6];
        for (var i = 0; i < code.Length; i++)
            code[i] = alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)];
        return $"IV-{DateTime.UtcNow:yyMMdd}-{new string(code)}";
    }

    // ── Presentación ─────────────────────────────────────────────────────────

    public static string Label(OrderStatus status) => status switch
    {
        OrderStatus.Pending   => "Pendiente",
        OrderStatus.Confirmed => "Confirmado",
        OrderStatus.Preparing => "En preparación",
        OrderStatus.Shipped   => "Enviado",
        OrderStatus.Delivered => "Entregado",
        OrderStatus.Cancelled => "Cancelado",
        OrderStatus.Refunded  => "Reembolsado",
        OrderStatus.Cart      => "Carrito",
        _ => status.ToString()
    };

    public static string Label(OrderSubStatus subStatus) => subStatus switch
    {
        OrderSubStatus.EnValidacion   => "En validación",
        OrderSubStatus.EnPreparacion  => "En preparación",
        OrderSubStatus.ListoParaEnvio => "Listo para envío",
        OrderSubStatus.Enviado        => "Enviado",
        OrderSubStatus.Entregado      => "Entregado",
        OrderSubStatus.Finalizado     => "Finalizado",
        OrderSubStatus.Rechazado      => "Rechazado",
        OrderSubStatus.Cancelado      => "Cancelado",
        _ => subStatus.ToString()
    };

    /// <summary>Clase CSS del badge (ver app.css: .st-*).</summary>
    public static string BadgeCss(OrderSubStatus subStatus) => subStatus switch
    {
        OrderSubStatus.EnValidacion   => "st-purple",
        OrderSubStatus.EnPreparacion  => "st-blue",
        OrderSubStatus.ListoParaEnvio => "st-orange",
        OrderSubStatus.Enviado        => "st-sky",
        OrderSubStatus.Entregado      => "st-green",
        OrderSubStatus.Finalizado     => "st-green",
        _                             => "st-red"
    };

    /// <summary>Pestaña del tablero a la que pertenece cada sub-estado.</summary>
    public static OrderBoardTab TabFor(OrderSubStatus subStatus) => subStatus switch
    {
        OrderSubStatus.EnValidacion => OrderBoardTab.Nuevos,
        OrderSubStatus.EnPreparacion or OrderSubStatus.ListoParaEnvio or OrderSubStatus.Enviado => OrderBoardTab.EnCurso,
        OrderSubStatus.Entregado or OrderSubStatus.Finalizado => OrderBoardTab.Completados,
        _ => OrderBoardTab.Cancelados
    };

    public static IReadOnlyList<OrderSubStatus> ColumnsFor(OrderBoardTab tab) => tab switch
    {
        OrderBoardTab.Nuevos      => [OrderSubStatus.EnValidacion],
        OrderBoardTab.EnCurso     => [OrderSubStatus.EnPreparacion, OrderSubStatus.ListoParaEnvio, OrderSubStatus.Enviado],
        OrderBoardTab.Completados => [OrderSubStatus.Entregado, OrderSubStatus.Finalizado],
        _                         => [OrderSubStatus.Rechazado, OrderSubStatus.Cancelado]
    };
}

public enum OrderBoardTab { Nuevos = 1, EnCurso = 2, Completados = 3, Cancelados = 4 }
