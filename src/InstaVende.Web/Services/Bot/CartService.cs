using System.Text;
using InstaVende.Core.Domain;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Services.Bot;

public sealed record CartOperationResult(bool Success, string Message, Order? Order = null);

/// <summary>
/// Carrito del bot por conversación (pedido con <see cref="OrderStatus.Cart"/>) y su
/// confirmación como pedido real. Compartido por los comandos rápidos y las herramientas de IA.
/// Los precios siempre se toman del catálogo, nunca del cliente ni del modelo.
/// </summary>
public class CartService
{
    public const int MaxQuantityPerItem = 99;

    private readonly AppDbContext _db;
    private readonly BusinessEvents _events;

    public CartService(AppDbContext db, BusinessEvents events)
    {
        _db = db;
        _events = events;
    }

    public Task<Order?> GetCartAsync(int businessId, int conversationId, CancellationToken ct = default)
        => _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.BusinessId == businessId
                                   && o.ConversationId == conversationId
                                   && o.Status == OrderStatus.Cart, ct);

    public async Task<CartOperationResult> AddItemAsync(BotContext ctx, int conversationId, int productId,
        int quantity, int? variantId = null, CancellationToken ct = default)
    {
        if (quantity is < 1 or > MaxQuantityPerItem)
            return new(false, $"La cantidad debe estar entre 1 y {MaxQuantityPerItem}.");

        var product = await _db.Products.AsNoTracking()
            .Include(p => p.Variants.Where(v => v.IsActive))
            .FirstOrDefaultAsync(p => p.Id == productId && p.BusinessId == ctx.BusinessId && p.IsActive, ct);
        if (product == null) return new(false, "El producto no existe o no está disponible.");

        var variant = variantId is null ? null : product.Variants.FirstOrDefault(v => v.Id == variantId);
        if (variantId is not null && variant == null) return new(false, "La variante indicada no existe.");

        var cart = await GetOrCreateCartAsync(ctx.BusinessId, conversationId, ct);
        if (cart == null) return new(false, "No se pudo crear el carrito para esta conversación.");

        var existing = cart.Items.FirstOrDefault(i => i.ProductId == product.Id && i.VariantDescription == VariantLabel(variant));
        var newQty = (existing?.Quantity ?? 0) + quantity;
        var available = variant?.Stock ?? product.Stock;
        if (newQty > available)
            return new(false, available <= 0
                ? $"{product.Name} está agotado."
                : $"Solo quedan {available} unidades de {product.Name}.");

        var unitPrice = product.Price + (variant?.PriceModifier ?? 0);
        if (existing != null)
        {
            existing.Quantity = newQty;
            existing.Subtotal = existing.UnitPrice * newQty;
        }
        else
        {
            cart.Items.Add(new OrderItem
            {
                ProductId          = product.Id,
                ProductName        = product.Name,
                VariantDescription = VariantLabel(variant),
                UnitPrice          = unitPrice,
                Quantity           = quantity,
                Subtotal           = unitPrice * quantity,
            });
        }
        Recalculate(cart, ctx, shippingCost: 0);
        await _db.SaveChangesAsync(ct);
        return new(true, $"{product.Name} agregado al carrito.", cart);
    }

    public async Task<bool> ClearAsync(int businessId, int conversationId, CancellationToken ct = default)
    {
        var cart = await GetCartAsync(businessId, conversationId, ct);
        if (cart == null) return false;
        _db.Orders.Remove(cart); // OrderItems se eliminan en cascada
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Convierte el carrito en un pedido "En validación" y actualiza las estadísticas del contacto.</summary>
    public async Task<CartOperationResult> ConfirmAsync(BotContext ctx, int conversationId, string? shippingAddress,
        string? paymentMethod, string? deliveryZone, string? notes, CancellationToken ct = default)
    {
        var cart = await GetCartAsync(ctx.BusinessId, conversationId, ct);
        if (cart == null || cart.Items.Count == 0)
            return new(false, "El carrito está vacío.");

        // Revalida stock y precios al momento de confirmar
        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.BusinessId == ctx.BusinessId)
            .ToDictionaryAsync(p => p.Id, ct);
        foreach (var item in cart.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var p) || !p.IsActive)
                return new(false, $"{item.ProductName} ya no está disponible.");
            if (item.VariantDescription == null && item.Quantity > p.Stock)
                return new(false, $"Solo quedan {p.Stock} unidades de {p.Name}.");
        }

        decimal shippingCost = 0;
        var zone = string.IsNullOrWhiteSpace(deliveryZone) ? null
            : ctx.DeliveryZones.FirstOrDefault(z => string.Equals(z.Name, deliveryZone.Trim(), StringComparison.OrdinalIgnoreCase));
        if (zone?.Cost is decimal zoneCost) shippingCost = zoneCost;

        Recalculate(cart, ctx, shippingCost);
        cart.ShippingAddress   = Truncate(shippingAddress, 500);
        cart.PaymentMethodName = Truncate(paymentMethod, 150);
        cart.Notes             = Truncate(string.Join(" | ", new[] { zone != null ? $"Zona: {zone.Name}" : null, notes }.Where(s => !string.IsNullOrWhiteSpace(s))), 1000);
        cart.OrderNumber       = OrderWorkflow.NewOrderNumber();
        cart.ChannelType       = await _db.Conversations.Where(c => c.Id == conversationId).Select(c => c.ChannelType).FirstAsync(ct);
        OrderWorkflow.ConfirmCart(cart);

        var total = cart.Total;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        await _db.Contacts.Where(c => c.Id == cart.ContactId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.TotalPurchases, c => c.TotalPurchases + 1)
                .SetProperty(c => c.TotalSpent, c => c.TotalSpent + total)
                .SetProperty(c => c.LastPurchaseAt, DateTime.UtcNow), ct);
        await tx.CommitAsync(ct);

        var customer = await _db.Contacts.AsNoTracking().Where(c => c.Id == cart.ContactId)
            .Select(c => c.Name ?? c.Phone ?? c.ExternalId).FirstAsync(ct);
        _events.Publish(new BusinessEvent(ctx.BusinessId, BusinessEventType.OrderCreated,
            $"Nuevo pedido {cart.OrderNumber}",
            $"{customer} confirmó un pedido por {ctx.Money(total)} ({cart.Items.Sum(i => i.Quantity)} unidades). Revisa el pago y prepáralo.",
            $"/orders/{cart.Id}", cart.ConversationId, cart.Id));
        return new(true, $"Pedido {cart.OrderNumber} registrado.", cart);
    }

    /// <summary>Resumen del carrito para el contexto del prompt y el comando #carrito.</summary>
    public static string Summarize(Order? cart, BotContext ctx)
    {
        if (cart == null || cart.Items.Count == 0) return "Vacío.";
        var sb = new StringBuilder();
        foreach (var i in cart.Items)
            sb.AppendLine($"- {i.ProductName}{(i.VariantDescription != null ? $" ({i.VariantDescription})" : "")} × {i.Quantity} = {ctx.Money(i.Subtotal)} [product_id {i.ProductId}]");
        sb.Append($"Subtotal: {ctx.Money(cart.Subtotal)}");
        if (cart.Discount > 0) sb.Append($" | Descuento: {ctx.Money(cart.Discount)}");
        return sb.ToString();
    }

    private async Task<Order?> GetOrCreateCartAsync(int businessId, int conversationId, CancellationToken ct)
    {
        var cart = await GetCartAsync(businessId, conversationId, ct);
        if (cart != null) return cart;

        var conv = await _db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId && c.BusinessId == businessId)
            .Select(c => new { c.ContactId, c.ChannelType })
            .FirstOrDefaultAsync(ct);
        if (conv == null) return null;

        cart = new Order
        {
            BusinessId     = businessId,
            ContactId      = conv.ContactId,
            ConversationId = conversationId,
            ChannelType    = conv.ChannelType,
            OrderNumber    = OrderWorkflow.NewOrderNumber(),
            Status         = OrderStatus.Cart,
            SubStatus      = OrderSubStatus.EnValidacion,
        };
        _db.Orders.Add(cart);
        return cart;
    }

    private static void Recalculate(Order order, BotContext ctx, decimal shippingCost)
    {
        order.Subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        var rules = ctx.Negotiation;
        order.ShippingCost = rules.FreeShippingThreshold > 0 && order.Subtotal >= rules.FreeShippingThreshold ? 0 : shippingCost;
        order.Discount = Math.Min(order.Discount, order.Subtotal);
        order.Total = order.Subtotal - order.Discount + order.ShippingCost;
        order.UpdatedAt = DateTime.UtcNow;
    }

    private static string? VariantLabel(ProductVariant? v) => v == null ? null : $"{v.AttributeName}: {v.AttributeValue}";

    private static string? Truncate(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}
