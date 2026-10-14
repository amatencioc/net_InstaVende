using InstaVende.Core.Enums;
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

        var totalConv = await _db.Conversations.CountAsync(c => c.BusinessId == biz.Id);

        // 3 queries de Orders consolidadas en 1 sola query agregada
        var orderStats = await _db.Orders
            .Where(o => o.BusinessId == biz.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total     = g.Count(),
                Revenue   = g.Where(o => o.Status != OrderStatus.Cancelled
                                      && o.Status != OrderStatus.Refunded)
                             .Sum(o => (decimal?)o.Total) ?? 0m,
                Delivered = g.Count(o => o.Status == OrderStatus.Delivered),
            })
            .FirstOrDefaultAsync();

        var totalOrders     = orderStats?.Total     ?? 0;
        var totalRevenue    = orderStats?.Revenue   ?? 0m;
        var completedOrders = orderStats?.Delivered ?? 0;
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
