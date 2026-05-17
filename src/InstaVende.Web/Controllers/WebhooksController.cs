using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Services;
using InstaVende.Web.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Controllers;

[ApiController]
[Route("api/webhooks")]
[IgnoreAntiforgeryToken]
public class WebhooksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBotEngineService _engine;
    private readonly IEnumerable<IChannelMessageSender> _senders;
    private readonly DataProtectionService _dp;
    private readonly IHubContext<InboxHub> _hub;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(AppDbContext db, IBotEngineService engine, IEnumerable<IChannelMessageSender> senders, DataProtectionService dp, IHubContext<InboxHub> hub, ILogger<WebhooksController> logger)
    { _db = db; _engine = engine; _senders = senders; _dp = dp; _hub = hub; _logger = logger; }

    [HttpGet("whatsapp/{merchantId:int}")]
    public async Task<IActionResult> VerifyWhatsApp(int merchantId, [FromQuery(Name = "hub.mode")] string mode, [FromQuery(Name = "hub.challenge")] string challenge, [FromQuery(Name = "hub.verify_token")] string verifyToken)
    {
        var cfg = await _db.ChannelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.BusinessId == merchantId && c.ChannelType == ChannelType.WhatsApp);
        if (cfg == null || cfg.WebhookVerifyToken != verifyToken || mode != "subscribe") return Forbid();
        return Ok(challenge);
    }

    [HttpPost("whatsapp/{merchantId:int}")]
    public async Task<IActionResult> WhatsAppEvent(int merchantId)
    {
        Request.EnableBuffering();
        using var sr = new StreamReader(Request.Body, leaveOpen: true);
        var body = await sr.ReadToEndAsync(); Request.Body.Position = 0;
        var cfg = await _db.ChannelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.BusinessId == merchantId
                                   && c.ChannelType == ChannelType.WhatsApp
                                   && c.IsActive);
        if (cfg == null) return Ok();
        if (!string.IsNullOrWhiteSpace(cfg.AppSecretEncrypted))
        {
            var secret = _dp.Decrypt(cfg.AppSecretEncrypted);
            var sig = Request.Headers["X-Hub-Signature-256"].ToString();
            if (!ValidateHmac(body, secret, sig)) { _logger.LogWarning("Bad HMAC for {Id}", merchantId); return Forbid(); }
        }
        try { await ProcessWhatsApp(merchantId, JsonDocument.Parse(body).RootElement); } catch (Exception ex) { _logger.LogError(ex, "WA payload error {Id}", merchantId); }
        return Ok();
    }

    // Local whatsapp-web.js client webhook
    // Expected payload: { "from": "521234567890", "body": "text", "businessId": 1, "pushname": "Juan" }
    [HttpPost("whatsapp-local/{merchantId:int}")]
    public async Task<IActionResult> WhatsAppLocalEvent(int merchantId)
    {
        try
        {
            using var sr  = new StreamReader(Request.Body);
            var raw       = await sr.ReadToEndAsync();
            using var doc = JsonDocument.Parse(raw);
            var root      = doc.RootElement;

            var from     = root.TryGetProperty("from",     out var f) ? f.GetString()?.Trim() : null;
            var text     = root.TryGetProperty("body",     out var b) ? b.GetString()?.Trim() : null;
            var pushname = root.TryGetProperty("pushname", out var p) ? p.GetString()?.Trim() : null;

            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(text))
                return Ok();

            // Verify the channel is active — use AsNoTracking since we don't modify cfg here
            var active = await _db.ChannelConfigs
                .AsNoTracking()
                .AnyAsync(c => c.BusinessId == merchantId
                            && c.ChannelType == ChannelType.WhatsApp
                            && c.IsActive);
            if (!active) return Ok();

            await HandleIncoming(merchantId, ChannelType.WhatsApp, from, text, pushname);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp local event error for business {Id}", merchantId);
        }
        return Ok();
    }

    [HttpGet("meta/{merchantId:int}")]
    public async Task<IActionResult> VerifyMeta(int merchantId, [FromQuery(Name = "hub.mode")] string mode, [FromQuery(Name = "hub.challenge")] string challenge, [FromQuery(Name = "hub.verify_token")] string verifyToken)
    {
        var cfg = await _db.ChannelConfigs
            .AsNoTracking()
            .Where(c => c.BusinessId == merchantId
                     && (c.ChannelType == ChannelType.FacebookMessenger
                      || c.ChannelType == ChannelType.Instagram))
            .FirstOrDefaultAsync(c => c.WebhookVerifyToken == verifyToken);
        if (cfg == null || mode != "subscribe") return Forbid();
        return Ok(challenge);
    }

    [HttpPost("meta/{merchantId:int}")]
    public async Task<IActionResult> MetaEvent(int merchantId)
    {
        Request.EnableBuffering();
        using var sr = new StreamReader(Request.Body, leaveOpen: true);
        var body = await sr.ReadToEndAsync(); Request.Body.Position = 0;

        var cfg = await _db.ChannelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.BusinessId == merchantId
                && (c.ChannelType == ChannelType.FacebookMessenger || c.ChannelType == ChannelType.Instagram)
                && c.IsActive);
        if (cfg != null && !string.IsNullOrWhiteSpace(cfg.AppSecretEncrypted))
        {
            var secret = _dp.Decrypt(cfg.AppSecretEncrypted);
            var sig = Request.Headers["X-Hub-Signature-256"].ToString();
            if (!ValidateHmac(body, secret, sig)) { _logger.LogWarning("Bad HMAC for Meta {Id}", merchantId); return Forbid(); }
        }

        try
        {
            var doc = JsonDocument.Parse(body).RootElement;
            var obj = doc.TryGetProperty("object", out var o) ? o.GetString() : null;
            if (obj == "page") await ProcessMessenger(merchantId, doc);
            else if (obj == "instagram") await ProcessInstagram(merchantId, doc);
        }
        catch (Exception ex) { _logger.LogError(ex, "Meta payload error {Id}", merchantId); }
        return Ok();
    }

    private async Task ProcessWhatsApp(int bid, JsonElement payload)
    {
        foreach (var entry in payload.GetProperty("entry").EnumerateArray())
        foreach (var change in entry.GetProperty("changes").EnumerateArray())
        {
            var value = change.GetProperty("value");
            if (!value.TryGetProperty("messages", out var msgs)) continue;
            foreach (var msg in msgs.EnumerateArray())
            {
                var from = msg.GetProperty("from").GetString()!;
                var text = msg.TryGetProperty("text", out var t) ? t.GetProperty("body").GetString()! : string.Empty;
                if (!string.IsNullOrEmpty(text)) await HandleIncoming(bid, ChannelType.WhatsApp, from, text);
            }
        }
    }

    private async Task ProcessMessenger(int bid, JsonElement payload)
    {
        foreach (var entry in payload.GetProperty("entry").EnumerateArray())
        foreach (var messaging in entry.GetProperty("messaging").EnumerateArray())
        {
            var sender = messaging.GetProperty("sender").GetProperty("id").GetString()!;
            if (!messaging.TryGetProperty("message", out var msg)) continue;
            var text = msg.TryGetProperty("text", out var t) ? t.GetString() : null;
            if (!string.IsNullOrEmpty(text)) await HandleIncoming(bid, ChannelType.FacebookMessenger, sender, text!);
        }
    }

    private async Task ProcessInstagram(int bid, JsonElement payload)
    {
        foreach (var entry in payload.GetProperty("entry").EnumerateArray())
        foreach (var messaging in entry.GetProperty("messaging").EnumerateArray())
        {
            var sender = messaging.GetProperty("sender").GetProperty("id").GetString()!;
            if (!messaging.TryGetProperty("message", out var msg)) continue;
            var text = msg.TryGetProperty("text", out var t) ? t.GetString() : null;
            if (!string.IsNullOrEmpty(text)) await HandleIncoming(bid, ChannelType.Instagram, sender, text!);
        }
    }

    private async Task HandleIncoming(
        int bid, ChannelType channel, string externalId, string text, string? pushname = null)
    {
        // ?? 1. Upsert contact ????????????????????????????????????????????????
        var contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.BusinessId == bid
                                   && c.ChannelType  == channel
                                   && c.ExternalId   == externalId);
        if (contact == null)
        {
            contact = new Contact
            {
                BusinessId = bid,
                ChannelType = channel,
                ExternalId  = externalId,
                Name        = pushname,
                Phone       = channel == ChannelType.WhatsApp ? externalId : null,
            };
            _db.Contacts.Add(contact);
        }
        else
        {
            contact.LastSeenAt = DateTime.UtcNow;
            // Update display name from pushname if we didn't have one yet
            if (!string.IsNullOrWhiteSpace(pushname) && string.IsNullOrWhiteSpace(contact.Name))
                contact.Name = pushname;
        }

        // ?? 2. Upsert conversation ???????????????????????????????????????????
        var conv = await _db.Conversations
            .Where(c => c.ContactId   == contact.Id
                     && c.ChannelType == channel
                     && c.Status      != ConversationStatus.Resolved)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync();

        if (conv == null)
        {
            // SaveChanges first to get contact.Id if it was just inserted
            await _db.SaveChangesAsync();
            conv = new Conversation { BusinessId = bid, ContactId = contact.Id, ChannelType = channel };
            _db.Conversations.Add(conv);
        }

        // ?? 3. Save inbound message ??????????????????????????????????????????
        var inMsg = new Message
        {
            ConversationId = conv.Id,
            Direction      = MessageDirection.Inbound,
            Content        = text,
        };
        _db.Messages.Add(inMsg);
        conv.UpdatedAt          = DateTime.UtcNow;
        conv.LastMessageAt      = DateTime.UtcNow;
        conv.LastMessagePreview = text.Length > 120 ? text[..120] + "..." : text;
        conv.UnreadCount++;
        await _db.SaveChangesAsync();   // single save for contact + conv + inMsg

        // ?? 4. Push inbound to SignalR ???????????????????????????????????????
        await _hub.Clients.Group($"business_{bid}").SendAsync("NewMessage", new
        {
            conversationId = conv.Id,
            messageId      = inMsg.Id,
            content        = inMsg.Content,
            direction      = (int)inMsg.Direction,
            sentAt         = inMsg.SentAt,
            contactName    = contact.Name ?? contact.ExternalId,
            channel        = (int)channel,
        });

        // ?? 5. Bot reply (only when bot is active) ???????????????????????????
        if (conv.Status != ConversationStatus.BotActive) return;

        var reply = await _engine.ProcessMessageAsync(bid, conv.Id, text);

        var outMsg = new Message
        {
            ConversationId = conv.Id,
            Direction      = MessageDirection.Outbound,
            Content        = reply,
            IsRead         = true,
            SentByBot      = true,
        };
        _db.Messages.Add(outMsg);
        // TransferirAHumanoAsync already called SaveChangesAsync and updated conv.Status
        // in the DB. Reload the tracked entity to pick up any status change made by tools.
        await _db.Entry(conv).ReloadAsync();
        // Reassign UpdatedAt after reload — ReloadAsync overwrites it with the stale DB value
        conv.UpdatedAt          = DateTime.UtcNow;
        conv.LastMessageAt      = DateTime.UtcNow;
        conv.LastMessagePreview = reply.Length > 120 ? reply[..120] + "..." : reply;
        await _db.SaveChangesAsync();

        // Send outbound via channel
        var sender = _senders.FirstOrDefault(s => s.Channel == channel);
        if (sender != null)
        {
            try   { await sender.SendTextAsync(bid, externalId, reply); }
            catch (Exception ex) { _logger.LogError(ex, "Send failed for {Channel} business {Id}", channel, bid); }
        }

        // Push outbound to SignalR
        await _hub.Clients.Group($"business_{bid}").SendAsync("NewMessage", new
        {
            conversationId = conv.Id,
            messageId      = outMsg.Id,
            content        = outMsg.Content,
            direction      = (int)outMsg.Direction,
            sentAt         = outMsg.SentAt,
            sentByBot      = true,
        });
    }

    private static bool ValidateHmac(string payload, string secret, string signature)
    {
        if (!signature.StartsWith("sha256=")) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(signature[7..]));
    }
}
