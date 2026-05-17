using System.Text;
using System.Text.Json;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Services;

/// <summary>
/// Sends reply messages to Instagram DMs via the Instagram Graph API.
/// Uses the same Messenger Send API endpoint but with the Instagram
/// Business Account's page access token and the recipient's Instagram-scoped ID.
/// </summary>
public class InstagramService : IChannelMessageSender
{
    public ChannelType Channel => ChannelType.Instagram;

    private readonly IHttpClientFactory _http;
    private readonly AppDbContext _db;
    private readonly DataProtectionService _dp;
    private readonly ILogger<InstagramService> _logger;
    private const string ApiBase = "https://graph.facebook.com/v20.0";

    public InstagramService(
        IHttpClientFactory http,
        AppDbContext db,
        DataProtectionService dp,
        ILogger<InstagramService> logger)
    {
        _http = http;
        _db = db;
        _dp = dp;
        _logger = logger;
    }

    public async Task SendTextAsync(int businessId, string instagramScopedId, string text)
    {
        var cfg = await _db.ChannelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.BusinessId == businessId &&
                c.ChannelType == ChannelType.Instagram &&
                c.IsActive);

        if (cfg == null)
        {
            _logger.LogWarning("No Instagram config found for business {BusinessId}", businessId);
            return;
        }

        var token    = _dp.Decrypt(cfg.AccessTokenEncrypted);
        var igUserId = cfg.InstagramAccountId;
        if (string.IsNullOrEmpty(igUserId))
        {
            _logger.LogWarning("InstagramAccountId not set for business {BusinessId}", businessId);
            return;
        }

        var payload = new
        {
            recipient = new { id = instagramScopedId },
            message   = new { text },
        };

        var client = _http.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var resp = await client.PostAsync($"{ApiBase}/{igUserId}/messages", content);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogError(
                "Instagram send failed for business {BusinessId}: {Status} - {Body}",
                businessId, resp.StatusCode, body);
        }
    }
}
