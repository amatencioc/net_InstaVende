using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;

    public DashboardController(AppDbContext db, CurrentUserService cu) { _db = db; _cu = cu; }

    public async Task<IActionResult> Index()
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Register", "Account");
        ViewBag.BusinessName = biz.Name;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetStats()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();
        var since = DateTime.UtcNow.AddDays(-30);
        var total = await _db.Conversations.CountAsync(c => c.BusinessId == bid);
        var resolved = await _db.Conversations.CountAsync(c => c.BusinessId == bid && c.Status == ConversationStatus.Resolved);
        var active = await _db.Conversations.CountAsync(c => c.BusinessId == bid && c.Status == ConversationStatus.BotActive);
        var products = await _db.Products.CountAsync(p => p.BusinessId == bid && p.IsActive);
        var byChannel = await _db.Conversations.Where(c => c.BusinessId == bid).GroupBy(c => c.ChannelType)
            .Select(g => new { Channel = g.Key.ToString(), Count = g.Count() }).ToListAsync();
        var daily = await _db.Conversations.Where(c => c.BusinessId == bid && c.CreatedAt >= since)
            .GroupBy(c => c.CreatedAt.Date).Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.Count() })
            .OrderBy(x => x.Date).ToListAsync();
        return Json(new { totalConversations = total, resolvedConversations = resolved, activeConversations = active, totalProducts = products, resolutionRate = total > 0 ? Math.Round((double)resolved / total * 100, 1) : 0, conversationsByChannel = byChannel, dailyConversations = daily });
    }
}
