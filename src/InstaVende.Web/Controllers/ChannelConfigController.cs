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
    private readonly WaClientHostedService _waService;

    public ChannelConfigController(
        AppDbContext db,
        CurrentUserService cu,
        DataProtectionService dp,
        IOptions<WhatsAppClientOptions> waOptions,
        IHttpClientFactory http,
        ILogger<ChannelConfigController> logger,
        IMemoryCache cache,
        WaClientHostedService waService)
    {
        _db        = db;
        _cu        = cu;
        _dp        = dp;
        _waOpts    = waOptions.Value;
        _http      = http;
        _logger    = logger;
        _cache     = cache;
        _waService = waService;
    }

    private const string WaOfflineCacheKey   = "wa_offline";
    private const string WaFailCountCacheKey = "wa_fail_count";
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

        // Solo usamos la caché offline para evitar saturar Node cuando está confirmado muerto
        // Y SOLO si el proceso ya no está intentando arrancar (no tenemos proceso vivo registrado).
        // Esto permite que los polls lleguen a Node durante el startup de Puppeteer (~30-60 s).
        bool processIsLaunching = !_waService.IsRestarting && _cache.TryGetValue(WaOfflineCacheKey, out _);
        if (processIsLaunching)
        {
            IncrementFailCounterAndMaybeRestart();
            return Content(offline, "application/json");
        }

        try
        {
            using var http = _http.CreateClient("wa-health");
            using var resp = await http.GetAsync($"{WaUrl}/status");
            if (!resp.IsSuccessStatusCode)
            {
                SetOfflineCache(TimeSpan.FromSeconds(4));
                IncrementFailCounterAndMaybeRestart();
                return Content(offline, "application/json");
            }
            var body = await resp.Content.ReadAsStringAsync();
            JsonDocument.Parse(body).Dispose(); // validate JSON before forwarding
            // Node respondió OK — limpiar toda la caché de fallos
            _cache.Remove(WaOfflineCacheKey);
            _cache.Remove(WaFailCountCacheKey);
            return Content(body, "application/json");
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            _logger.LogDebug("WA client offline at {Url} (expected when not running)", WaUrl);
            SetOfflineCache(TimeSpan.FromSeconds(4));
            IncrementFailCounterAndMaybeRestart();
            return Content(offline, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WA client unexpected error at {Url}", WaUrl);
            SetOfflineCache(TimeSpan.FromSeconds(4));
            IncrementFailCounterAndMaybeRestart();
            return Content(offline, "application/json");
        }
    }

    /// <summary>
    /// Incrementa el contador de fallos consecutivos y dispara el auto-restart
    /// cuando se alcanza <see cref="WhatsAppClientOptions.OfflineFailThreshold"/>.
    /// Se llama tanto en fallos HTTP reales como en polls con caché offline activa,
    /// de modo que el umbral se evalúa en cada poll independientemente del TTL del caché.
    /// </summary>
    private void IncrementFailCounterAndMaybeRestart()
    {
        var threshold = Math.Max(1, _waOpts.OfflineFailThreshold);
        var fails = _cache.GetOrCreate(WaFailCountCacheKey, e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return 0;
        });
        fails++;
        _cache.Set(WaFailCountCacheKey, fails, TimeSpan.FromMinutes(2));

        if (fails >= threshold)
        {
            _logger.LogWarning(
                "WaStatus: Node offline for {Count} consecutive polls (threshold {Threshold}) — triggering auto-restart.",
                fails, threshold);
            _cache.Remove(WaFailCountCacheKey);
            _ = _waService.RestartIfDeadAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Devuelve si hay un reinicio del proceso Node en curso y el número de
    /// fallos consecutivos actuales, para que la UI pueda mostrar un contador
    /// de reintentos junto al mensaje "Reiniciando servicio…".
    /// GET /ChannelConfig/WaRestartStatus
    /// </summary>
    [HttpGet]
    public IActionResult WaRestartStatus()
    {
        var failCount = _cache.TryGetValue(WaFailCountCacheKey, out int f) ? f : 0;
        return Json(new
        {
            restarting = _waService.IsRestarting,
            failCount,
        });
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
            _cache.Remove(WaFailCountCacheKey);
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
            // Delegate to the hosted service so the process is tracked under _process.
            // waitSeconds=0 means "fire and return" — the browser poll loop will pick up the QR.
            await _waService.EnsureRunningAsync(CancellationToken.None);

            // Clear stale caches so WaStatus starts hitting Node immediately.
            _cache.Remove(WaOfflineCacheKey);
            _cache.Remove(WaFailCountCacheKey);

            _logger.LogInformation("WaRestart: EnsureRunningAsync completed — Node launching.");
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

    /// <summary>
    /// Elimina la sesión de whatsapp-web.js guardada en disco para forzar
    /// un QR fresco la próxima vez que el cliente Node arranque.
    /// Útil cuando la sesión está corrupta o expirada y el QR nunca aparece.
    /// POST /ChannelConfig/ClearWaSession
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ClearWaSession()
    {
        var sessionPath = Path.Combine(WaClientPath, "session");
        try
        {
            // Detener Node antes de borrar la sesión para evitar conflictos con Chrome
            _waService.StopNode();
            _logger.LogInformation("ClearWaSession: Node process stopped before session deletion.");

            if (Directory.Exists(sessionPath))
            {
                Directory.Delete(sessionPath, recursive: true);
                _logger.LogInformation("ClearWaSession: session directory deleted ({Path})", sessionPath);
            }
            else
            {
                _logger.LogInformation("ClearWaSession: no session directory found at {Path}", sessionPath);
            }

            // Clear caches so the next WaStatus poll hits Node fresh
            _cache.Remove(WaOfflineCacheKey);
            _cache.Remove(WaFailCountCacheKey);

            return Json(new { ok = true, message = "Sesión eliminada. El próximo inicio generará un QR fresco." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClearWaSession: failed to delete session directory {Path}", sessionPath);
            return Json(new { ok = false, message = $"No se pudo eliminar la sesión: {ex.Message}" });
        }
    }
}
