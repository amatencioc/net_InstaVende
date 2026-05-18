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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Controllers;

[Authorize]
public class ChannelConfigController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;
    private readonly DataProtectionService _dp;
    private readonly WhatsAppClientOptions _waOpts;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ChannelConfigController> _logger;
    private readonly IMemoryCache _cache;

    public ChannelConfigController(
        AppDbContext db,
        CurrentUserService cu,
        DataProtectionService dp,
        IOptions<WhatsAppClientOptions> waOptions,
        IHttpClientFactory http,
        ILogger<ChannelConfigController> logger,
        IMemoryCache cache)
    {
        _db     = db;
        _cu     = cu;
        _dp     = dp;
        _waOpts = waOptions.Value;
        _http   = http;
        _logger = logger;
        _cache  = cache;
    }

    private const string WaOfflineCacheKey = "wa_offline";
    private string WaUrl        => _waOpts.BaseUrl;
    private string WaClientPath => Path.IsPathRooted(_waOpts.ClientPath)
        ? _waOpts.ClientPath
        : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _waOpts.ClientPath));

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

        // Skip live check if wa-client was recently confirmed offline
        if (!_cache.TryGetValue(WaOfflineCacheKey, out _))
        {
            try
            {
                using var http = _http.CreateClient("wa-health");
                using var resp = await http.GetAsync($"{WaUrl}/status");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("connected", out var c) && c.GetBoolean())
                        return Json(new { connected = true, source = "live" });
                }
            }
            catch
            {
                _cache.Set(WaOfflineCacheKey, true, TimeSpan.FromSeconds(12));
            }
        }

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
        const string offline = "{\"state\":\"disconnected\",\"connected\":false,\"qrDataUrl\":null,\"qrExpiresAt\":null,\"info\":null}";

        if (_cache.TryGetValue(WaOfflineCacheKey, out _))
            return Content(offline, "application/json");

        try
        {
            using var http = _http.CreateClient("wa-health");
            using var resp = await http.GetAsync($"{WaUrl}/status");
            if (!resp.IsSuccessStatusCode)
            {
                SetOfflineCache(TimeSpan.FromSeconds(4));
                return Content(offline, "application/json");
            }
            var body = await resp.Content.ReadAsStringAsync();
            JsonDocument.Parse(body).Dispose(); // validate JSON before forwarding
            return Content(body, "application/json");
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            _logger.LogDebug("WA client offline at {Url} (expected when not running)", WaUrl);
            SetOfflineCache(TimeSpan.FromSeconds(4));
            return Content(offline, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WA client unexpected error at {Url}", WaUrl);
            SetOfflineCache(TimeSpan.FromSeconds(4));
            return Content(offline, "application/json");
        }
    }

    private void SetOfflineCache(TimeSpan ttl)
        => _cache.Set(WaOfflineCacheKey, true, ttl);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect()
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();

        // Tell the local WA client to log out (best-effort)
        try
        {
            using var http = _http.CreateClient("wa-send");
            using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            await http.PostAsync($"{WaUrl}/disconnect", content);
        }
        catch { /* ignore — Node may be offline */ }

        // Clear any cached offline/online state so next page visit reflects reality
        _cache.Remove(WaOfflineCacheKey);

        // Mark inactive in DB
        var existing = await _db.ChannelConfigs
            .FirstOrDefaultAsync(c => c.BusinessId == bid && c.ChannelType == ChannelType.WhatsApp);
        if (existing != null) { existing.IsActive = false; existing.ConnectedAt = null; await _db.SaveChangesAsync(); }

        TempData["Success"] = "WhatsApp desconectado correctamente.";
        return RedirectToAction("WhatsApp");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> WaRestart()
    {
        // Check if already running — if so just clear cache and return
        if (await WaIsReachableAsync())
        {
            _cache.Remove(WaOfflineCacheKey);
            return Json(new { ok = true, message = "El servicio ya estaba corriendo." });
        }

        var waPath  = WaClientPath;
        var indexJs = Path.Combine(waPath, "index.js");

        if (!Directory.Exists(waPath))
            return Json(new { ok = false, message = $"Directorio wa-client no encontrado: {waPath}" });
        if (!System.IO.File.Exists(indexJs))
            return Json(new { ok = false, message = "index.js no encontrado en wa-client." });

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = "node",
                Arguments              = "index.js",
                WorkingDirectory       = waPath,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            var proc = new System.Diagnostics.Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogInformation("[wa-client] {Line}", e.Data); };
            proc.ErrorDataReceived  += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogWarning("[wa-client] {Line}", e.Data); };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            _logger.LogInformation("WaRestart: launched wa-client (PID {Pid}) from {Path}", proc.Id, waPath);

            // Clear the offline cache so WaStatus proxy stops returning stale "offline" JSON.
            _cache.Remove(WaOfflineCacheKey);

            // Return immediately — the browser's poll loop (WaStatus) will pick up the QR
            // as soon as Node generates it (~15-20 s).
            return Json(new { ok = true, message = "Servicio iniciado. Generando codigo QR..." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WaRestart: failed to start wa-client");
            return Json(new { ok = false, message = "No se pudo iniciar el servicio. Verifica que Node.js este instalado." });
        }
    }

    private async Task<bool> WaIsReachableAsync()
    {
        try
        {
            using var http = _http.CreateClient("wa-health");
            using var resp = await http.GetAsync($"{WaUrl}/health");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
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
