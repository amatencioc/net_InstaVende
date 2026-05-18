using System.Text;
using System.Text.Json;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Services;

public class MetaMessengerService : IChannelMessageSender
{
    public ChannelType Channel => ChannelType.FacebookMessenger;
    private readonly IHttpClientFactory _http;
    private readonly AppDbContext _db;
    private readonly DataProtectionService _dp;
    private readonly ILogger<MetaMessengerService> _logger;
    private const string ApiBase = "https://graph.facebook.com/v20.0";

    public MetaMessengerService(IHttpClientFactory http, AppDbContext db, DataProtectionService dp, ILogger<MetaMessengerService> logger)
    { _http = http; _db = db; _dp = dp; _logger = logger; }

    public async Task SendTextAsync(int businessId, string psid, string text)
    {
        var cfg = await _db.ChannelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.BusinessId == businessId
                && c.ChannelType == ChannelType.FacebookMessenger && c.IsActive);
        if (cfg == null) { _logger.LogWarning("No Messenger config for {Id}", businessId); return; }

        var token = _dp.Decrypt(cfg.AccessTokenEncrypted);
        var payload = new { recipient = new { id = psid }, message = new { text } };
        using var client = _http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await client.PostAsync($"{ApiBase}/me/messages", content);
        if (!resp.IsSuccessStatusCode)
            _logger.LogError("Messenger send failed: {S} {B}", resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }
}
