using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Controllers;

[Authorize]
public class MetricasController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;

    public MetricasController(AppDbContext db, CurrentUserService cu)
    {
        _db = db;
        _cu = cu;
    }

    public async Task<IActionResult> Index()
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Register", "Account");

        var totalConv   = await _db.Conversations.CountAsync(c => c.BusinessId == biz.Id);
        var totalOrders = await _db.Orders.CountAsync(o => o.BusinessId == biz.Id);
        var totalRevenue = await _db.Orders
            .Where(o => o.BusinessId == biz.Id && o.Status != InstaVende.Core.Enums.OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total) ?? 0;
        var completedOrders = await _db.Orders.CountAsync(o => o.BusinessId == biz.Id && o.Status == InstaVende.Core.Enums.OrderStatus.Delivered);
        var convRate = totalConv > 0 ? (double)completedOrders / totalConv : 0;

        var vm = new MetricasViewModel
        {
            HasAccess         = true,
            TotalConversations = totalConv,
            TotalOrders        = totalOrders,
            TotalRevenue       = totalRevenue,
            ConversionRate     = convRate,
            CompletedOrders    = completedOrders,
        };

        return View(vm);
    }
}
