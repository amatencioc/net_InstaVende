using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InstaVende.Core.Enums;

namespace InstaVende.Web.Services.Messaging;

/// <summary>Verificación de firmas HMAC-SHA256 ("sha256=&lt;hex&gt;") sobre los bytes crudos del cuerpo.</summary>
public static class WebhookSignature
{
    public static bool IsValid(ReadOnlySpan<byte> body, string? secret, string? header)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header)) return false;
        if (!header.StartsWith("sha256=", StringComparison.Ordinal)) return false;

        Span<byte> expected = stackalloc byte[32];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body, expected);

        byte[] provided;
        try { provided = Convert.FromHexString(header.AsSpan(7)); }
        catch (FormatException) { return false; }
        return provided.Length == 32 && CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    public static bool FixedTimeEquals(string? a, string? b)
        => a != null && b != null &&
           CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(a)), SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}

/// <summary>Convierte los payloads de cada proveedor en <see cref="InboundMessage"/>. Nunca lanza por campos ausentes.</summary>
public static class WebhookPayloadParser
{
    /// <summary>WhatsApp Cloud API: entry[].changes[].value.messages[] (+ contacts[] para el nombre).</summary>
    public static IEnumerable<InboundMessage> ParseWhatsAppCloud(int businessId, JsonElement root)
    {
        foreach (var value in EnumerateChangeValues(root))
        {
            if (!value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array) continue;

            var names = new Dictionary<string, string>();
            if (value.TryGetProperty("contacts", out var contacts) && contacts.ValueKind == JsonValueKind.Array)
                foreach (var c in contacts.EnumerateArray())
                    if (Str(c, "wa_id") is { } waId && c.TryGetProperty("profile", out var profile) && Str(profile, "name") is { } name)
                        names[waId] = name;

            foreach (var msg in messages.EnumerateArray())
            {
                var from = Str(msg, "from");
                var text = Str(msg, "type") switch
                {
                    "text"        => msg.TryGetProperty("text", out var t) ? Str(t, "body") : null,
                    "button"      => msg.TryGetProperty("button", out var b) ? Str(b, "text") : null,
                    "interactive" => msg.TryGetProperty("interactive", out var i) ? InteractiveTitle(i) : null,
                    _             => null,
                };
                if (from == null || string.IsNullOrWhiteSpace(text)) continue;
                yield return new InboundMessage(businessId, ChannelType.WhatsApp, from, Str(msg, "id"), text.Trim(), names.GetValueOrDefault(from));
            }
        }
    }

    /// <summary>Messenger e Instagram: entry[].messaging[] con message.text; ignora ecos y eventos sin texto.</summary>
    public static IEnumerable<InboundMessage> ParseMetaMessaging(int businessId, JsonElement root, ChannelType channel)
    {
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array) yield break;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("messaging", out var events) || events.ValueKind != JsonValueKind.Array) continue;
            foreach (var ev in events.EnumerateArray())
            {
                if (!ev.TryGetProperty("message", out var msg)) continue;
                if (msg.TryGetProperty("is_echo", out var echo) && echo.ValueKind == JsonValueKind.True) continue;
                var sender = ev.TryGetProperty("sender", out var s) ? Str(s, "id") : null;
                var text = Str(msg, "text");
                if (sender == null || string.IsNullOrWhiteSpace(text)) continue;
                yield return new InboundMessage(businessId, channel, sender, Str(msg, "mid"), text.Trim(), null);
            }
        }
    }

    /// <summary>wa-client (sesión QR): { from, body, pushname, messageId }.</summary>
    public static InboundMessage? ParseWhatsAppLocal(int businessId, JsonElement root)
    {
        var from = Str(root, "from");
        var body = Str(root, "body");
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(body)) return null;
        return new InboundMessage(businessId, ChannelType.WhatsApp, from.Trim(), Str(root, "messageId"), body.Trim(), Str(root, "pushname"));
    }

    private static IEnumerable<JsonElement> EnumerateChangeValues(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array) yield break;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array) continue;
            foreach (var change in changes.EnumerateArray())
                if (change.TryGetProperty("value", out var value)) yield return value;
        }
    }

    private static string? InteractiveTitle(JsonElement interactive)
        => interactive.TryGetProperty("button_reply", out var br) ? Str(br, "title")
         : interactive.TryGetProperty("list_reply", out var lr) ? Str(lr, "title")
         : null;

    private static string? Str(JsonElement el, string key)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
