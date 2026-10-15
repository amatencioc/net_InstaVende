using InstaVende.Core.Domain;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using InstaVende.Web.Services.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Orders;

/// <summary>
/// Gestión de pedidos del panel: tablero kanban por pestañas, detalle, cambio de estado,
/// edición de datos administrativos y exportación CSV.
/// Los pedidos los crea el bot (no hay alta manual) y los carritos (<see cref="OrderStatus.Cart"/>)
/// nunca se exponen como pedidos.
/// </summary>
[Route("api/orders")]
public sealed class OrdersController(
    AppDbContext db,
    MessageDispatcher dispatcher,
    ILogger<OrdersController> logger) : TenantController
{
    /// <summary>Máximo de tarjetas devueltas por columna (el contador refleja el total real).</summary>
    private const int ColumnLimit = 100;
    private const int MaxSearchLength = 100;

    // ── Flujo ────────────────────────────────────────────────────────────────

    /// <summary>Pestañas, columnas y etiquetas del flujo de pedidos (fuente: OrderWorkflow).</summary>
    [HttpGet("workflow")]
    [ProducesResponseType<OrderWorkflowDto>(StatusCodes.Status200OK)]
    public ActionResult<OrderWorkflowDto> Workflow()
        => new OrderWorkflowDto(OrderPresentation.Tabs
            .Select(tab => new OrderWorkflowTabDto(tab, OrderPresentation.TabLabel(tab),
                OrderWorkflow.ColumnsFor(tab).Select(SubStatusInfo).ToList()))
            .ToList());

    // ── Tablero ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Columnas de la pestaña indicada con sus pedidos (más recientes primero) y contadores de cada pestaña.
    /// <paramref name="from"/>/<paramref name="to"/> son fechas locales del negocio (intervalo [from, to + 1 día)).
    /// </summary>
    [HttpGet("board")]
    [ProducesResponseType<OrderBoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderBoardDto>> Board(
        [FromQuery] OrderBoardTab tab = OrderBoardTab.Nuevos,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(tab)) return BadRequestProblem("La pestaña indicada no existe.");
        if (ValidateFilters(search, from, to) is { } invalid) return invalid;

        var filtered = Filtered(search, from, to);

        // 1 consulta: contadores e importes por sub-estado (alimenta pestañas y columnas)
        var stats = await filtered
            .GroupBy(o => o.SubStatus)
            .Select(g => new { SubStatus = g.Key, Count = g.Count(), Amount = g.Sum(o => o.Total) })
            .ToListAsync(ct);
        var bySub = stats.ToDictionary(s => s.SubStatus);

        // 1 consulta por columna de la pestaña (máx. 3), limitada y ya proyectada
        var columns = new List<OrderBoardColumnDto>();
        foreach (var sub in OrderWorkflow.ColumnsFor(tab))
        {
            var count = bySub.TryGetValue(sub, out var s) ? s.Count : 0;
            var cards = count == 0
                ? []
                : await ProjectCards(filtered.Where(o => o.SubStatus == sub)
                        .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
                        .Take(ColumnLimit))
                    .ToListAsync(ct);

            columns.Add(new OrderBoardColumnDto(sub, OrderWorkflow.Label(sub), OrderWorkflow.BadgeCss(sub),
                count, s?.Amount ?? 0m, cards.Select(ToCard).ToList()));
        }

        var tabCounts = OrderPresentation.Tabs
            .Select(t => new OrderBoardTabCountDto(t, OrderPresentation.TabLabel(t),
                stats.Where(x => OrderWorkflow.TabFor(x.SubStatus) == t).Sum(x => x.Count)))
            .ToList();

        return new OrderBoardDto(tab, columns, tabCounts);
    }

    // ── Detalle ──────────────────────────────────────────────────────────────

    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailDto>> Get(int id, CancellationToken ct)
    {
        var detail = await LoadDetailAsync(id, ct);
        return detail is null ? NotFoundProblem("El pedido no existe o fue eliminado.") : detail;
    }

    // ── Cambio de estado ─────────────────────────────────────────────────────

    /// <summary>
    /// Mueve el pedido a otro sub-estado (validado con OrderWorkflow) y, si se pide y el pedido
    /// tiene conversación, avisa al cliente por su canal.
    /// </summary>
    [HttpPost("{id:int}/move")]
    [ProducesResponseType<OrderMoveResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderMoveResultDto>> Move(int id, OrderMoveRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.SubStatus)) return BadRequestProblem("El estado de destino no es válido.");

        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.Id == id && o.BusinessId == BusinessId && o.Status != OrderStatus.Cart, ct);
        if (order is null) return NotFoundProblem("El pedido no existe o fue eliminado.");
        if (!OrderWorkflow.CanMove(order, request.SubStatus))
            return ConflictProblem($"El pedido ya está en «{OrderWorkflow.Label(request.SubStatus)}».");

        var wasRevenue = OrderWorkflow.RevenueStatuses.Contains(order.Status);
        OrderWorkflow.MoveTo(order, request.SubStatus);
        var isRevenue = OrderWorkflow.RevenueStatuses.Contains(order.Status);

        if (wasRevenue == isRevenue)
        {
            await db.SaveChangesAsync(ct);
        }
        else
        {
            // Al cancelar o reactivar un pedido se mantienen coherentes las estadísticas del cliente.
            // La transacción va dentro de la estrategia de reintentos configurada (EnableRetryOnFailure).
            var delta = isRevenue ? 1 : -1;
            var amount = isRevenue ? order.Total : -order.Total;
            var contactId = order.ContactId;
            var businessId = BusinessId;
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await db.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
                await db.Contacts
                    .Where(c => c.Id == contactId && c.BusinessId == businessId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.TotalPurchases, c => c.TotalPurchases + delta < 0 ? 0 : c.TotalPurchases + delta)
                        .SetProperty(c => c.TotalSpent, c => c.TotalSpent + amount < 0 ? 0m : c.TotalSpent + amount), ct);
                await tx.CommitAsync(ct);
            });
            db.ChangeTracker.AcceptAllChanges();
        }

        var notified = false;
        string? notificationError = null;
        if (request.NotifyCustomer)
        {
            if (order.ConversationId is not int conversationId)
                notificationError = "El pedido no tiene una conversación asociada para avisar al cliente.";
            else
            {
                try
                {
                    var text = $"Tu pedido {order.OrderNumber} está ahora: {OrderWorkflow.Label(order.SubStatus)}";
                    var message = await dispatcher.SendAsync(BusinessId, conversationId, text, sentByBot: false, ct);
                    notified = message.DeliveryStatus == MessageDeliveryStatus.Sent;
                    if (!notified) notificationError = "No se pudo entregar el aviso al cliente por su canal.";
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "No se pudo avisar al cliente del pedido {OrderId}", order.Id);
                    notificationError = "No se pudo enviar el aviso al cliente.";
                }
            }
        }

        return new OrderMoveResultDto(order.Id, order.Status, order.SubStatus, OrderWorkflow.Label(order.SubStatus),
            OrderWorkflow.BadgeCss(order.SubStatus), OrderWorkflow.TabFor(order.SubStatus), order.UpdatedAt,
            notified, notificationError);
    }

    // ── Edición administrativa ───────────────────────────────────────────────

    /// <summary>Actualiza notas, dirección de entrega y método de pago (reemplaza los tres valores).</summary>
    [HttpPatch("{id:int}")]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailDto>> Update(int id, OrderUpdateRequest request, CancellationToken ct)
    {
        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.Id == id && o.BusinessId == BusinessId && o.Status != OrderStatus.Cart, ct);
        if (order is null) return NotFoundProblem("El pedido no existe o fue eliminado.");

        order.Notes             = Clean(request.Notes);
        order.ShippingAddress   = Clean(request.ShippingAddress);
        order.PaymentMethodName = Clean(request.PaymentMethodName);
        order.UpdatedAt         = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return (await LoadDetailAsync(id, ct))!;
    }

    // ── Exportación ──────────────────────────────────────────────────────────

    /// <summary>
    /// Exporta a CSV (UTF-8 con BOM) los pedidos que cumplen los filtros; sin <paramref name="tab"/> exporta todos.
    /// Fechas en la zona horaria del negocio.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export(
        [FromQuery] OrderBoardTab? tab = null,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
    {
        if (tab is { } t && !Enum.IsDefined(t)) return BadRequestProblem("La pestaña indicada no existe.");
        if (ValidateFilters(search, from, to) is { } invalid) return invalid;

        var query = Filtered(search, from, to);
        if (tab is { } selected)
        {
            var subs = OrderWorkflow.ColumnsFor(selected).ToArray();
            query = query.Where(o => subs.Contains(o.SubStatus));
        }

        var rows = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Select(o => new
            {
                o.OrderNumber, o.CreatedAt, o.SubStatus, o.Status, o.ChannelType,
                ContactName = o.Contact.Name, o.Contact.Phone,
                o.Subtotal, o.Discount, o.ShippingCost, o.Total,
                o.PaymentMethodName, o.ShippingAddress, o.Notes,
                Items = o.Items.OrderBy(i => i.Id)
                    .Select(i => new { i.ProductName, i.VariantDescription, i.Quantity })
                    .ToList(),
            })
            .AsSplitQuery()
            .ToListAsync(ct);

        var csv = new OrderCsvWriter();
        foreach (var header in new[]
                 {
                     "Número", "Fecha", "Cliente", "Teléfono", "Canal", "Estado", "Sub-estado", "Productos",
                     "Unidades", "Subtotal", "Descuento", "Envío", "Total", "Método de pago", "Dirección", "Notas"
                 })
            csv.Text(header);
        csv.EndLine();

        foreach (var r in rows)
        {
            var products = string.Join("; ", r.Items.Select(i =>
                $"{i.ProductName}{(string.IsNullOrWhiteSpace(i.VariantDescription) ? "" : $" ({i.VariantDescription})")} x{i.Quantity}"));
            csv.Text(r.OrderNumber)
               .Text(Tenant.ToLocal(r.CreatedAt).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture))
               .Text(r.ContactName)
               .Text(r.Phone)
               .Text(ChannelLabel(r.ChannelType))
               .Text(OrderWorkflow.Label(r.Status))
               .Text(OrderWorkflow.Label(r.SubStatus))
               .Text(products)
               .Integer(r.Items.Sum(i => i.Quantity))
               .Amount(r.Subtotal)
               .Amount(r.Discount)
               .Amount(r.ShippingCost)
               .Amount(r.Total)
               .Text(r.PaymentMethodName)
               .Text(r.ShippingAddress)
               .Text(r.Notes)
               .EndLine();
        }

        var fileName = $"pedidos-{Tenant.ToLocal(DateTime.UtcNow):yyyyMMdd}.csv";
        return File(csv.ToBytes(), "text/csv; charset=utf-8", fileName);
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────

    /// <summary>Pedidos reales del negocio (sin carritos) con búsqueda y rango de fechas locales.</summary>
    private IQueryable<Order> Filtered(string? search, DateOnly? from, DateOnly? to)
    {
        var businessId = BusinessId;
        var query = db.Orders.AsNoTracking()
            .Where(o => o.BusinessId == businessId && o.Status != OrderStatus.Cart);

        if (from is { } f)
        {
            var fromUtc = Tenant.ToUtc(f.ToDateTime(TimeOnly.MinValue));
            query = query.Where(o => o.CreatedAt >= fromUtc);
        }
        if (to is { } t)
        {
            var toUtc = Tenant.ToUtc(t.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(o => o.CreatedAt < toUtc);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o =>
                o.OrderNumber.Contains(term) ||
                (o.Contact.Name != null && o.Contact.Name.Contains(term)) ||
                (o.Contact.Phone != null && o.Contact.Phone.Contains(term)));
        }
        return query;
    }

    private ObjectResult? ValidateFilters(string? search, DateOnly? from, DateOnly? to)
    {
        if (search is { Length: > MaxSearchLength })
            return BadRequestProblem($"La búsqueda no puede superar {MaxSearchLength} caracteres.");
        if (from is { } f && to is { } t && f > t)
            return BadRequestProblem("La fecha inicial no puede ser posterior a la final.");
        return null;
    }

    private static IQueryable<OrderCardRow> ProjectCards(IQueryable<Order> query)
        => query.Select(o => new OrderCardRow(
            o.Id,
            o.OrderNumber,
            o.ContactId,
            o.Contact.Name ?? o.Contact.Phone ?? o.Contact.ExternalId,
            o.ChannelType,
            o.Total,
            o.Items.Sum(i => (int?)i.Quantity) ?? 0,
            o.CreatedAt,
            o.SubStatus,
            o.ConversationId));

    private static OrderCardDto ToCard(OrderCardRow r)
        => new(r.Id, r.OrderNumber, r.ContactId, r.ContactName, r.Channel, r.Total, r.ItemCount, r.CreatedAt,
            r.SubStatus, OrderWorkflow.Label(r.SubStatus), OrderWorkflow.BadgeCss(r.SubStatus), r.ConversationId);

    private async Task<OrderDetailDto?> LoadDetailAsync(int id, CancellationToken ct)
    {
        var businessId = BusinessId;
        var row = await db.Orders.AsNoTracking()
            .Where(o => o.Id == id && o.BusinessId == businessId && o.Status != OrderStatus.Cart)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.Status, o.SubStatus, o.ChannelType,
                o.Subtotal, o.Discount, o.ShippingCost, o.Total,
                o.PaymentMethodName, o.ShippingAddress, o.Notes, o.CreatedAt, o.UpdatedAt, o.ConversationId,
                Contact = new OrderContactDto(o.Contact.Id, o.Contact.Name ?? o.Contact.Phone ?? o.Contact.ExternalId,
                    o.Contact.Phone, o.Contact.ChannelType, o.Contact.AvatarUrl),
                Items = o.Items.OrderBy(i => i.Id)
                    .Select(i => new OrderLineDto(i.Id, i.ProductId, i.ProductName, i.VariantDescription,
                        i.Product.ImageUrl, i.Quantity, i.UnitPrice, i.Subtotal))
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;
        return new OrderDetailDto(row.Id, row.OrderNumber, row.Status, OrderWorkflow.Label(row.Status),
            row.SubStatus, OrderWorkflow.Label(row.SubStatus), OrderWorkflow.BadgeCss(row.SubStatus),
            OrderWorkflow.TabFor(row.SubStatus), row.ChannelType, row.Subtotal, row.Discount, row.ShippingCost,
            row.Total, row.PaymentMethodName, row.ShippingAddress, row.Notes, row.CreatedAt, row.UpdatedAt,
            row.ConversationId, row.Contact, row.Items);
    }

    private static OrderSubStatusInfoDto SubStatusInfo(OrderSubStatus sub)
    {
        var status = OrderWorkflow.StatusFor(sub);
        return new OrderSubStatusInfoDto(sub, OrderWorkflow.Label(sub), OrderWorkflow.BadgeCss(sub),
            OrderWorkflow.TabFor(sub), status, OrderWorkflow.RevenueStatuses.Contains(status));
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ChannelLabel(ChannelType channel) => channel switch
    {
        ChannelType.WhatsApp          => "WhatsApp",
        ChannelType.FacebookMessenger => "Messenger",
        ChannelType.Instagram         => "Instagram",
        _ => channel.ToString()
    };

    private sealed record OrderCardRow(
        int Id, string OrderNumber, int ContactId, string ContactName, ChannelType Channel, decimal Total,
        int ItemCount, DateTime CreatedAt, OrderSubStatus SubStatus, int? ConversationId);
}
