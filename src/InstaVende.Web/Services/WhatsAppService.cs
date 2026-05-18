using System.Text;
using System.Text.Json;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Services;

/// <summary>
/// Sends WhatsApp messages via the local whatsapp-web.js Node.js client.
/// Expected endpoint: POST {BaseUrl}/send  { to: "phone", message: "text" }
/// </summary>
public class WhatsAppService : IChannelMessageSender
{
    public ChannelType Channel => ChannelType.WhatsApp;

    private readonly IHttpClientFactory _http;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(IHttpClientFactory http, AppDbContext db, IConfiguration config, ILogger<WhatsAppService> logger)
    { _http = http; _db = db; _config = config; _logger = logger; }

    public async Task SendTextAsync(int businessId, string recipient, string text)
    {
        var active = await _db.ChannelConfigs
            .AsNoTracking()
            .AnyAsync(c => c.BusinessId == businessId
                        && c.ChannelType == ChannelType.WhatsApp
                        && c.IsActive);

        if (!active)
        {
            _logger.LogWarning("No active WhatsApp config for business {Id}", businessId);
            return;
        }

        var waUrl   = _config["WhatsAppClient:BaseUrl"] ?? "http://localhost:3001";
        var payload = JsonSerializer.Serialize(new { to = recipient, message = text });

        // Reuse named client (timeout configured at registration in Program.cs)
        using var http = _http.CreateClient("wa-send");

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var resp = await http.PostAsync(
                    $"{waUrl}/send",
                    new StringContent(payload, Encoding.UTF8, "application/json"));

                if (resp.IsSuccessStatusCode) return;

                var body = await resp.Content.ReadAsStringAsync();
                _logger.LogError(
                    "WhatsApp send failed (attempt {Attempt}) for business {Id}: {Status} — {Body}",
                    attempt, businessId, resp.StatusCode, body);

                // 503 = WA client not connected — no point retrying
                if ((int)resp.StatusCode == 503) return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "WhatsApp send exception (attempt {Attempt}) for business {Id} to {Recipient}",
                    attempt, businessId, recipient);
            }

            if (attempt < 2) await Task.Delay(1500);
        }
    }
}
