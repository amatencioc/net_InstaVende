using System.Net.Http.Headers;
using System.Text.Json;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Services;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InstaVende.Web.Controllers;

[Authorize]
public class ChannelConfigController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;
    private readonly DataProtectionService _dp;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public ChannelConfigController(AppDbContext db, CurrentUserService cu, DataProtectionService dp, IConfiguration config, IHttpClientFactory http)
    { _db = db; _cu = cu; _dp = dp; _config = config; _http = http; }

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

    [HttpGet]
    public async Task<IActionResult> IsConnected()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Json(new { connected = false });
        var cfg = await _db.ChannelConfigs
            .FirstOrDefaultAsync(c => c.BusinessId == bid && c.ChannelType == ChannelType.WhatsApp);
        return Json(new { connected = cfg?.IsActive == true });
    }

    // ?? Embedded Signup landing page
    [HttpGet]
    public async Task<IActionResult> WhatsApp()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return RedirectToAction("Register", "Account");
        var cfg = await _db.ChannelConfigs
            .FirstOrDefaultAsync(c => c.BusinessId == bid && c.ChannelType == ChannelType.WhatsApp);
        ViewBag.IsConnected = cfg?.IsActive == true;
        ViewBag.PhoneNumberId = cfg?.PhoneNumberId;
        ViewBag.PhoneNumber = cfg?.PhoneNumber;
        ViewBag.ConnectedAt = cfg?.ConnectedAt;
        ViewBag.MetaAppId = _config["Meta:AppId"] ?? "";
        ViewBag.EmbeddedSignupConfigId = _config["Meta:EmbeddedSignupConfigId"] ?? "";
        return View();
    }

    // ?? Receive code from Embedded Signup JS and persist the channel ????????
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EmbeddedSignup([FromBody] EmbeddedSignupCallbackViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(model.Code))
            return BadRequest(new { error = "No se recibió el código de autorización." });

        var appId = _config["Meta:AppId"];
        var appSecret = _config["Meta:AppSecret"];
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appSecret))
            return BadRequest(new { error = "Meta App no configurada en el servidor." });

        try
        {
            var client = _http.CreateClient();

            // 1. Exchange code ? access token
            var tokenUrl = $"https://graph.facebook.com/v20.0/oauth/access_token" +
                           $"?client_id={appId}&client_secret={appSecret}&code={Uri.EscapeDataString(model.Code)}";
            var tokenResp = await client.GetStringAsync(tokenUrl);
            using var tokenDoc = JsonDocument.Parse(tokenResp);
            if (!tokenDoc.RootElement.TryGetProperty("access_token", out var tokenEl))
                return BadRequest(new { error = "No se pudo obtener el access token de Meta." });
            var accessToken = tokenEl.GetString()!;

            // 2. Resolve phone number id — prefer what JS already gave us
            var phoneNumberId = model.PhoneNumberId;
            if (string.IsNullOrEmpty(phoneNumberId) && !string.IsNullOrEmpty(model.WabaId))
            {
                var phoneUrl = $"https://graph.facebook.com/v20.0/{model.WabaId}/phone_numbers?access_token={accessToken}";
                var phoneResp = await client.GetStringAsync(phoneUrl);
                using var phoneDoc = JsonDocument.Parse(phoneResp);
                if (phoneDoc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.GetArrayLength() > 0)
                    phoneNumberId = dataEl[0].GetProperty("id").GetString();
            }

            // 3. Persist ChannelConfig
            var existing = await _db.ChannelConfigs
                .FirstOrDefaultAsync(c => c.BusinessId == bid && c.ChannelType == ChannelType.WhatsApp);
            if (existing == null)
            {
                _db.ChannelConfigs.Add(new ChannelConfig
                {
                    BusinessId = bid.Value,
                    ChannelType = ChannelType.WhatsApp,
                    PhoneNumberId = phoneNumberId,
                    AccessTokenEncrypted = _dp.Encrypt(accessToken),
                    WebhookVerifyToken = Guid.NewGuid().ToString("N")[..16],
                    IsActive = true
                });
            }
            else
            {
                if (!string.IsNullOrEmpty(phoneNumberId)) existing.PhoneNumberId = phoneNumberId;
                existing.AccessTokenEncrypted = _dp.Encrypt(accessToken);
                existing.IsActive = true;
            }
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
