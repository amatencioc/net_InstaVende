using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace InstaVende.Web.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _user;

    public OrdersController(AppDbContext db, CurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<IActionResult> Index(OrderStatus? status, int? contactId, DateTime? from, DateTime? to)
    {
        var biz = await _user.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Index", "Dashboard");

        var q = _db.Orders
            .Include(o => o.Contact)
            .Include(o => o.Items)
            .Where(o => o.BusinessId == biz.Id);

        if (status.HasValue) q = q.Where(o => o.Status == status.Value);
        if (contactId.HasValue) q = q.Where(o => o.ContactId == contactId.Value);
        if (from.HasValue) q = q.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(o => o.CreatedAt <= to.Value.AddDays(1));

        var orders = await q.OrderByDescending(o => o.CreatedAt).ToListAsync();

        ViewBag.StatusFilter = status;
        ViewBag.TotalOrders = orders.Count;
        ViewBag.TotalRevenue = orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.Total);
        ViewBag.PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending);

        return View(orders.Select(o => MapToVm(o)).ToList());
    }

    public async Task<IActionResult> Detail(int id)
    {
        var biz = await _user.GetBusinessAsync();
        var order = await _db.Orders
            .Include(o => o.Contact)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Conversation)
            .FirstOrDefaultAsync(o => o.Id == id && o.BusinessId == biz!.Id);

        if (order == null) return NotFound();
        return View(MapToVm(order));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
    {
        var biz = await _user.GetBusinessAsync();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.BusinessId == biz!.Id);
        if (order == null) return Json(new { ok = false });

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Json(new { ok = true, statusLabel = MapToVm(order).StatusLabel });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderViewModel vm)
    {
        var biz = await _user.GetBusinessAsync();
        if (biz == null) return Json(new { ok = false });

        var order = new Order
        {
            BusinessId = biz.Id,
            ContactId = vm.ContactId,
            ConversationId = vm.ConversationId,
            OrderNumber = GenerateOrderNumber(),
            ChannelType = vm.ChannelType,
            PaymentMethodName = vm.PaymentMethodName,
            ShippingAddress = vm.ShippingAddress,
            Notes = vm.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var item in vm.Items)
        {
            var product = await _db.Products.FindAsync(item.ProductId);
            if (product == null) continue;
            var oi = new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = product.Name,
                VariantDescription = item.VariantDescription,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Quantity * item.UnitPrice
            };
            order.Items.Add(oi);
        }

        order.Subtotal = order.Items.Sum(i => i.Subtotal);
        order.Total = order.Subtotal - order.Discount + order.ShippingCost;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Actualizar estadísticas del contacto
        var contact = await _db.Contacts.FindAsync(vm.ContactId);
        if (contact != null)
        {
            contact.TotalPurchases++;
            contact.TotalSpent += order.Total;
            contact.LastPurchaseAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Json(new { ok = true, id = order.Id, orderNumber = order.OrderNumber });
    }

    [HttpGet]
    public async Task<IActionResult> GetStats()
    {
        var biz = await _user.GetBusinessAsync();
        if (biz == null) return Json(new { });

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var orders = await _db.Orders.Where(o => o.BusinessId == biz.Id).ToListAsync();

        return Json(new
        {
            totalMonth = orders.Count(o => o.CreatedAt >= monthStart),
            revenueMonth = orders.Where(o => o.CreatedAt >= monthStart && o.Status != OrderStatus.Cancelled).Sum(o => o.Total),
            pending = orders.Count(o => o.Status == OrderStatus.Pending),
            delivered = orders.Count(o => o.Status == OrderStatus.Delivered)
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSubStatus([FromBody] UpdateSubStatusRequest req)
    {
        var biz = await _user.GetBusinessAsync();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == req.Id && o.BusinessId == biz!.Id);
        if (order == null) return Json(new { ok = false });
        order.SubStatus = (OrderSubStatus)req.SubStatus;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(OrderSubStatus? subStatus)
    {
        var biz = await _user.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Index", "Dashboard");

        var q = _db.Orders.Include(o => o.Contact).Include(o => o.Items).Where(o => o.BusinessId == biz.Id);
        if (subStatus.HasValue) q = q.Where(o => o.SubStatus == subStatus.Value);
        var orders = await q.OrderByDescending(o => o.CreatedAt).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("N°Pedido,Estado,Sub-estado,Cliente,Total,Fecha");
        foreach (var o in orders)
        {
            var vm = MapToVm(o);
            sb.AppendLine($"\"{o.OrderNumber}\",\"{vm.StatusLabel}\",\"{vm.SubStatusLabel}\",\"{o.Contact?.Name ?? ""}\",{o.Total},{o.CreatedAt:yyyy-MM-dd}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"pedidos-{DateTime.Now:yyyyMMdd}.csv");
    }

    private static string GenerateOrderNumber() =>
        $"ORD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";

    private static OrderViewModel MapToVm(Order o) => new()
    {
        Id = o.Id, OrderNumber = o.OrderNumber, Status = o.Status, SubStatus = o.SubStatus,
        ContactId = o.ContactId, ContactName = o.Contact?.Name,
        ContactPhone = o.Contact?.Phone, ConversationId = o.ConversationId,
        Subtotal = o.Subtotal, Discount = o.Discount,
        ShippingCost = o.ShippingCost, Total = o.Total,
        PaymentMethodName = o.PaymentMethodName,
        ShippingAddress = o.ShippingAddress, Notes = o.Notes,
        ChannelType = o.ChannelType, CreatedAt = o.CreatedAt,
        Items = o.Items.Select(i => new OrderItemViewModel
        {
            Id = i.Id, ProductId = i.ProductId, ProductName = i.ProductName,
            VariantDescription = i.VariantDescription, Quantity = i.Quantity,
            UnitPrice = i.UnitPrice, Subtotal = i.Subtotal
        }).ToList()
    };
public class IdOnlyRequestOrders { public int Id { get; set; } }
public class UpdateSubStatusRequest { public int Id { get; set; } public int SubStatus { get; set; } }
}
