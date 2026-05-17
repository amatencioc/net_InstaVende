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

namespace InstaVende.Web.Controllers;

[Authorize]
public class ChannelConfigController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;
    private readonly DataProtectionService _dp;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ChannelConfigController> _logger;

    public ChannelConfigController(AppDbContext db, CurrentUserService cu, DataProtectionService dp, IConfiguration config, IHttpClientFactory http, ILogger<ChannelConfigController> logger)
    { _db = db; _cu = cu; _dp = dp; _config = config; _http = http; _logger = logger; }

    public async Task<IActionResult> Index()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return RedirectToAction("Register", "Account");
        var cfgs = await _db.ChannelConfigs
            .AsNoTracking()
            .Where(c => c.BusinessId == bid)
            .ToListAsync();
        return View(cfgs.Select(c => new ChannelConfigViewModel
        {
            Id                  = c.Id,
            ChannelType         = c.ChannelType,
            PhoneNumberId       = c.PhoneNumberId,
            PageId              = c.PageId,
            InstagramAccountId  = c.InstagramAccountId,
            AccessToken         = "***",
            AppSecret           = string.IsNullOrEmpty(c.AppSecretEncrypted) ? null : "***",
            WebhookVerifyToken  = c.WebhookVerifyToken ?? string.Empty,
            IsActive            = c.IsActive,
        }));
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] ChannelConfigViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var existing = await _db.ChannelConfigs.FirstOrDefaultAsync(c => c.BusinessId == bid && c.ChannelType == model.ChannelType);
        if (existing == null)
        {
            if (string.IsNullOrWhiteSpace(model.AccessToken) || model.AccessToken == "***")
                return BadRequest(new { error = "El token de acceso es obligatorio al crear un canal." });
            _db.ChannelConfigs.Add(new ChannelConfig
            {
                BusinessId           = bid.Value,
                ChannelType          = model.ChannelType,
                PhoneNumberId        = model.PhoneNumberId,
                PageId               = model.PageId,
                InstagramAccountId   = model.InstagramAccountId,
                AccessTokenEncrypted = _dp.Encrypt(model.AccessToken),
                AppSecretEncrypted   = model.AppSecret != null ? _dp.Encrypt(model.AppSecret) : null,
                WebhookVerifyToken   = model.WebhookVerifyToken,
                IsActive             = model.IsActive,
            });
        }
        else
        {
            existing.PhoneNumberId       = model.PhoneNumberId;
            existing.PageId              = model.PageId;
            existing.InstagramAccountId  = model.InstagramAccountId;
            if (!string.IsNullOrWhiteSpace(model.AccessToken) && model.AccessToken != "***")
                existing.AccessTokenEncrypted = _dp.Encrypt(model.AccessToken);
            if (!string.IsNullOrWhiteSpace(model.AppSecret) && model.AppSecret != "***")
                existing.AppSecretEncrypted = _dp.Encrypt(model.AppSecret);
            existing.WebhookVerifyToken = model.WebhookVerifyToken;
            existing.IsActive           = model.IsActive;
        }
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> IsConnected()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Json(new { connected = false });

        // Check live status first (most accurate)
        var waUrl = _config["WhatsAppClient:BaseUrl"] ?? "http://localhost:3001";
        try
        {
            using var http = _http.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(3);
            using var resp = await http.GetAsync($"{waUrl}/status");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("connected", out var c) && c.GetBoolean())
                    return Json(new { connected = true, source = "live" });
            }
        }
        catch { /* WA client offline — fall back to DB */ }

        // Fallback: DB flag
        var cfg = await _db.ChannelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.BusinessId == bid && c.ChannelType == ChannelType.WhatsApp);
        return Json(new { connected = cfg?.IsActive == true, source = "db" });
    }

    // QR-based WhatsApp connection page (uses local whatsapp-web.js client)
    [HttpGet]
    public async Task<IActionResult> WhatsApp()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return RedirectToAction("Register", "Account");
        var cfg = await _db.ChannelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.BusinessId == bid && c.ChannelType == ChannelType.WhatsApp);
        ViewBag.IsConnected = cfg?.IsActive == true;
        ViewBag.PhoneNumber  = cfg?.PhoneNumber;
        ViewBag.ConnectedAt  = cfg?.ConnectedAt;
        return View();
    }

    /// <summary>
    /// Server-side proxy to the local whatsapp-web.js Node client.
    /// Avoids CORS — the browser calls this endpoint, .NET calls Node.
    /// GET /ChannelConfig/WaStatus
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> WaStatus()
    {
        var waUrl = _config["WhatsAppClient:BaseUrl"] ?? "http://localhost:3001";
        try
        {
            using var http = _http.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(12);
            using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            using var resp = await http.GetAsync($"{waUrl}/status", cts.Token);
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            return Content(body, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WA client unreachable at {Url}", waUrl);
            return Content(
                """{"state":"disconnected","connected":false,"qrDataUrl":null,"qrExpiresAt":null,"info":null,"error":"WA client offline"}""",
                "application/json");
        }
    }

    // Disconnect the local WA session and mark ChannelConfig inactive
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();

        // Tell the local WA client to log out (best-effort)
        try
        {
            var waUrl = _config["WhatsAppClient:BaseUrl"] ?? "http://localhost:3001";
            using var http = _http.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(6);
            using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            await http.PostAsync($"{waUrl}/disconnect", content);
        }
        catch { /* ignore — Node may be offline */ }

        // Mark inactive in DB
        var existing = await _db.ChannelConfigs
            .FirstOrDefaultAsync(c => c.BusinessId == bid && c.ChannelType == ChannelType.WhatsApp);
        if (existing != null) { existing.IsActive = false; await _db.SaveChangesAsync(); }

        TempData["Success"] = "WhatsApp desconectado correctamente.";
        return RedirectToAction("WhatsApp");
    }

    // Called by the QR page JS when the local WA client reports connected
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQrConnection([FromBody] QrConnectionViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();

        var existing = await _db.ChannelConfigs
            .FirstOrDefaultAsync(c => c.BusinessId == bid && c.ChannelType == ChannelType.WhatsApp);

        // Normalize phone: strip +, spaces, @c.us suffix that Node may include
        var phone = model.Phone?
            .Replace("@c.us", "")
            .Replace("+", "")
            .Replace(" ", "")
            .Trim();
        if (string.IsNullOrWhiteSpace(phone)) phone = null;

        if (existing == null)
        {
            _db.ChannelConfigs.Add(new ChannelConfig
            {
                BusinessId           = bid.Value,
                ChannelType          = ChannelType.WhatsApp,
                PhoneNumber          = phone,
                AccessTokenEncrypted = string.Empty,   // QR-based — no Meta token needed
                WebhookVerifyToken   = Guid.NewGuid().ToString("N")[..16],
                ConnectedAt          = DateTime.UtcNow,
                IsActive             = true,
            });
        }
        else
        {
            if (phone != null) existing.PhoneNumber = phone;
            existing.ConnectedAt = DateTime.UtcNow;
            existing.IsActive    = true;
        }

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }
}
