using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Services;

public class WhatsAppService : IChannelMessageSender
{
    public ChannelType Channel => ChannelType.WhatsApp;
    private readonly IHttpClientFactory _http;
    private readonly AppDbContext _db;
    private readonly DataProtectionService _dp;
    private readonly ILogger<WhatsAppService> _logger;
    private const string ApiBase = "https://graph.facebook.com/v20.0";

    public WhatsAppService(IHttpClientFactory http, AppDbContext db, DataProtectionService dp, ILogger<WhatsAppService> logger)
    { _http = http; _db = db; _dp = dp; _logger = logger; }

    public async Task SendTextAsync(int businessId, string recipient, string text)
    {
        var cfg = await _db.ChannelConfigs.FirstOrDefaultAsync(c => c.BusinessId == businessId
            && c.ChannelType == ChannelType.WhatsApp && c.IsActive);
        if (cfg == null) { _logger.LogWarning("No WhatsApp config for {Id}", businessId); return; }

        var token = _dp.Decrypt(cfg.AccessTokenEncrypted);
        var payload = new { messaging_product = "whatsapp", to = recipient, type = "text", text = new { body = text } };
        var client = _http.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.PostAsync($"{ApiBase}/{cfg.PhoneNumberId}/messages",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        if (!resp.IsSuccessStatusCode)
            _logger.LogError("WhatsApp send failed: {S} {B}", resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }
}
