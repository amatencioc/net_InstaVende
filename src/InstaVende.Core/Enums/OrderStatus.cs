namespace InstaVende.Core.Enums;

/// <summary>
/// Estado agregado del pedido. Se deriva siempre de <see cref="OrderSubStatus"/>
/// mediante <c>OrderWorkflow</c>; no asignarlo a mano salvo para <see cref="Cart"/>.
/// </summary>
public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Preparing = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6,
    Refunded = 7,
    /// <summary>Carrito en construcción por el bot. No es un pedido: no aparece en el tablero ni cuenta como ingreso.</summary>
    Cart = 8
}
