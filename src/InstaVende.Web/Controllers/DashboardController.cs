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

        // Una única query agrupada reemplaza las 3 CountAsync independientes sobre Conversations
        var convStats = await _db.Conversations
            .AsNoTracking()
            .Where(c => c.BusinessId == bid)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total    = g.Count(),
                Resolved = g.Count(c => c.Status == ConversationStatus.Resolved),
                Active   = g.Count(c => c.Status == ConversationStatus.BotActive),
            })
            .FirstOrDefaultAsync();

        var total    = convStats?.Total    ?? 0;
        var resolved = convStats?.Resolved ?? 0;
        var active   = convStats?.Active   ?? 0;

        var products = await _db.Products.CountAsync(p => p.BusinessId == bid && p.IsActive);
        var byChannelMapped = (await _db.Conversations
            .AsNoTracking()
            .Where(c => c.BusinessId == bid)
            .GroupBy(c => c.ChannelType)
            .Select(g => new { Channel = g.Key, Count = g.Count() })
            .ToListAsync())
            .Select(x => new { Channel = x.Channel.ToString(), x.Count }).ToList();
        var dailyMapped = (await _db.Conversations
            .AsNoTracking()
            .Where(c => c.BusinessId == bid && c.CreatedAt >= since)
            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month, c.CreatedAt.Day })
            .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Day = g.Key.Day, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day)
            .ToListAsync())
            .Select(x => new { Date = new DateTime(x.Year, x.Month, x.Day).ToString("yyyy-MM-dd"), x.Count })
            .ToList();

        return Json(new
        {
            totalConversations     = total,
            resolvedConversations  = resolved,
            activeConversations    = active,
            totalProducts          = products,
            resolutionRate         = total > 0 ? Math.Round((double)resolved / total * 100, 1) : 0,
            conversationsByChannel = byChannelMapped,
            dailyConversations     = dailyMapped,
        });
    }
}
