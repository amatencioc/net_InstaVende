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

        // Plan free: no access to detailed metrics (paywall)
        var vm = new MetricasViewModel
        {
            HasAccess = false,
            TotalConversations = await _db.Conversations.CountAsync(c => c.BusinessId == biz.Id),
            TotalOrders = await _db.Orders.CountAsync(o => o.BusinessId == biz.Id),
            TotalRevenue = await _db.Orders
                .Where(o => o.BusinessId == biz.Id && o.Status != InstaVende.Core.Enums.OrderStatus.Cancelled)
                .SumAsync(o => (decimal?)o.Total) ?? 0
        };

        return View(vm);
    }
}
