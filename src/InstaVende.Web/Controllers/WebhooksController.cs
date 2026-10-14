using System.Text.Json;
using InstaVende.Core.Enums;
using InstaVende.Web.Infrastructure;
using InstaVende.Web.Services.Channels;
using InstaVende.Web.Services.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Controllers;

/// <summary>
/// Webhooks de Meta (WhatsApp Cloud, Messenger, Instagram) y de wa-client (sesiones QR).
/// Solo validan la firma sobre los bytes crudos, normalizan y encolan: el procesamiento
/// (bot incluido) ocurre en segundo plano y la respuesta es inmediata para evitar reintentos.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[IgnoreAntiforgeryToken]
[EnableRateLimiting(RateLimitPolicies.Webhooks)]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class WebhooksController(
    ChannelConfigProvider channels,
    InboundMessageQueue queue,
    IOptions<WhatsAppClientOptions> waOptions,
    ILogger<WebhooksController> logger) : ControllerBase
{
    private const int MaxBodyBytes = 1024 * 1024;

    // ── Verificación de suscripción (GET hub.challenge) ──────────────────────

    [HttpGet("whatsapp/{businessId:int}")]
    public Task<IActionResult> VerifyWhatsApp(int businessId,
        [FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.challenge")] string? challenge,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken)
        => VerifyAsync(businessId, [ChannelType.WhatsApp], mode, challenge, verifyToken);

    [HttpGet("meta/{businessId:int}")]
    public Task<IActionResult> VerifyMeta(int businessId,
        [FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.challenge")] string? challenge,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken)
        => VerifyAsync(businessId, [ChannelType.FacebookMessenger, ChannelType.Instagram], mode, challenge, verifyToken);

    private async Task<IActionResult> VerifyAsync(int businessId, ChannelType[] candidates, string? mode, string? challenge, string? verifyToken)
    {
        if (mode != "subscribe" || string.IsNullOrEmpty(challenge)) return StatusCode(StatusCodes.Status403Forbidden);
        foreach (var channel in candidates)
        {
            var cfg = await channels.GetAsync(businessId, channel, HttpContext.RequestAborted);
            if (cfg != null && WebhookSignature.FixedTimeEquals(cfg.WebhookVerifyToken, verifyToken))
                return Content(challenge, "text/plain");
        }
        return StatusCode(StatusCodes.Status403Forbidden);
    }

    // ── Eventos ──────────────────────────────────────────────────────────────

    [HttpPost("whatsapp/{businessId:int}"), RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> WhatsAppCloud(int businessId)
    {
        var cfg = await channels.GetAsync(businessId, ChannelType.WhatsApp, HttpContext.RequestAborted);
        if (cfg is not { IsActive: true }) return Ok(); // canal no configurado: se acepta y se ignora

        var body = await ReadBodyAsync();
        if (!WebhookSignature.IsValid(body, cfg.AppSecret, Request.Headers["X-Hub-Signature-256"]))
            return Rejected(businessId, "WhatsApp Cloud", cfg.AppSecret == null);

        return Enqueue(Parse(body, root => WebhookPayloadParser.ParseWhatsAppCloud(businessId, root)));
    }

    [HttpPost("meta/{businessId:int}"), RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Meta(int businessId)
    {
        var body = await ReadBodyAsync();
        var channel = ReadObjectType(body) switch
        {
            "page"      => ChannelType.FacebookMessenger,
            "instagram" => ChannelType.Instagram,
            _           => (ChannelType?)null,
        };
        if (channel == null) return Ok();

        var cfg = await channels.GetAsync(businessId, channel.Value, HttpContext.RequestAborted);
        if (cfg is not { IsActive: true }) return Ok();
        if (!WebhookSignature.IsValid(body, cfg.AppSecret, Request.Headers["X-Hub-Signature-256"]))
            return Rejected(businessId, channel.Value.ToString(), cfg.AppSecret == null);

        return Enqueue(Parse(body, root => WebhookPayloadParser.ParseMetaMessaging(businessId, root, channel.Value)));
    }

    /// <summary>Mensajes de las sesiones QR, firmados por wa-client con el secreto compartido.</summary>
    [HttpPost("whatsapp-local/{businessId:int}"), RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> WhatsAppLocal(int businessId)
    {
        var body = await ReadBodyAsync();
        if (!WebhookSignature.IsValid(body, waOptions.Value.SharedSecret, Request.Headers["X-WA-Signature"]))
            return Rejected(businessId, "wa-client", secretMissing: false);

        var message = Parse(body, root => WebhookPayloadParser.ParseWhatsAppLocal(businessId, root) is { } m ? [m] : []);
        return Enqueue(message);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<byte[]> ReadBodyAsync()
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, HttpContext.RequestAborted);
        return ms.ToArray();
    }

    private IReadOnlyList<InboundMessage> Parse(byte[] body, Func<JsonElement, IEnumerable<InboundMessage>> parser)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return parser(doc.RootElement).ToList();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Payload de webhook inválido");
            return [];
        }
    }

    private static string? ReadObjectType(byte[] body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("object", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private IActionResult Enqueue(IReadOnlyList<InboundMessage> messages)
    {
        foreach (var m in messages)
        {
            if (!queue.TryEnqueue(m))
            {
                logger.LogError("Cola de mensajes entrantes llena; se pide reintento al proveedor.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }
        return Ok();
    }

    private IActionResult Rejected(int businessId, string source, bool secretMissing)
    {
        if (secretMissing)
            logger.LogWarning("Webhook {Source} rechazado para el negocio {BusinessId}: el canal no tiene App Secret configurado.", source, businessId);
        else
            logger.LogWarning("Webhook {Source} rechazado para el negocio {BusinessId}: firma inválida.", source, businessId);
        return Unauthorized();
    }
}
