using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Services;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Controllers;

[Authorize]
public class ChannelConfigController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;
    private readonly DataProtectionService _dp;

    public ChannelConfigController(AppDbContext db, CurrentUserService cu, DataProtectionService dp)
    { _db = db; _cu = cu; _dp = dp; }

    public async Task<IActionResult> Index()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return RedirectToAction("Register", "Account");
        var cfgs = await _db.ChannelConfigs.Where(c => c.BusinessId == bid).ToListAsync();
        return View(cfgs.Select(c => new ChannelConfigViewModel { Id = c.Id, ChannelType = c.ChannelType, PhoneNumberId = c.PhoneNumberId, PageId = c.PageId, InstagramAccountId = c.InstagramAccountId, AccessToken = "***", AppSecret = string.IsNullOrEmpty(c.AppSecretEncrypted) ? null : "***", WebhookVerifyToken = c.WebhookVerifyToken ?? string.Empty, IsActive = c.IsActive }));
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] ChannelConfigViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var existing = await _db.ChannelConfigs.FirstOrDefaultAsync(c => c.BusinessId == bid && c.ChannelType == model.ChannelType);
        if (existing == null)
            _db.ChannelConfigs.Add(new ChannelConfig { BusinessId = bid.Value, ChannelType = model.ChannelType, PhoneNumberId = model.PhoneNumberId, PageId = model.PageId, InstagramAccountId = model.InstagramAccountId, AccessTokenEncrypted = _dp.Encrypt(model.AccessToken), AppSecretEncrypted = model.AppSecret != null ? _dp.Encrypt(model.AppSecret) : null, WebhookVerifyToken = model.WebhookVerifyToken, IsActive = model.IsActive });
        else
        {
            existing.PhoneNumberId = model.PhoneNumberId; existing.PageId = model.PageId; existing.InstagramAccountId = model.InstagramAccountId;
            if (model.AccessToken != "***") existing.AccessTokenEncrypted = _dp.Encrypt(model.AccessToken);
            if (model.AppSecret != null && model.AppSecret != "***") existing.AppSecretEncrypted = _dp.Encrypt(model.AppSecret);
            existing.WebhookVerifyToken = model.WebhookVerifyToken; existing.IsActive = model.IsActive;
        }
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }
}
