using InstaVende.Core.Enums;
namespace InstaVende.Core.Entities;

public class Order
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int ContactId { get; set; }
    public int? ConversationId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public OrderSubStatus SubStatus { get; set; } = OrderSubStatus.EnValidacion;
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public string? PaymentMethodName { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
    public ChannelType ChannelType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Business Business { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
    public Conversation? Conversation { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
