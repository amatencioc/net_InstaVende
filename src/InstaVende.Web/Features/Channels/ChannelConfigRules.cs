using System.Security.Cryptography;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Services;
using InstaVende.Web.Services.Channels;

namespace InstaVende.Web.Features.Channels;

/// <summary>Reglas puras del módulo de canales (sin acceso a datos).</summary>
internal static class ChannelConfigRules
{
    /// <summary>Estado que se informa cuando wa-client no responde.</summary>
    public const string OfflineState = "offline";

    /// <summary>Token de verificación del webhook: 192 bits aleatorios en hexadecimal.</summary>
    public static string NewVerifyToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    /// <summary>
    /// WhatsApp usa la Cloud API cuando tiene PhoneNumberId y token (misma regla que
    /// <c>ChannelSnapshot.IsWhatsAppCloud</c>); en otro caso es una sesión QR.
    /// </summary>
    public static bool IsWhatsAppCloud(ChannelConfig cfg)
        => cfg.ChannelType == ChannelType.WhatsApp
           && !string.IsNullOrWhiteSpace(cfg.PhoneNumberId)
           && !string.IsNullOrWhiteSpace(cfg.AccessTokenEncrypted);

    public static bool IsWhatsAppQrActive(ChannelConfig? cfg)
        => cfg is { ChannelType: ChannelType.WhatsApp, IsActive: true } && !IsWhatsAppCloud(cfg);

    public static bool IsWhatsAppCloudActive(ChannelConfig? cfg)
        => cfg is { ChannelType: ChannelType.WhatsApp, IsActive: true } && IsWhatsAppCloud(cfg);

    /// <summary>Identificador de Meta obligatorio para cada canal.</summary>
    public static string? MetaAccountId(ChannelConfig cfg) => cfg.ChannelType switch
    {
        ChannelType.WhatsApp          => cfg.PhoneNumberId,
        ChannelType.FacebookMessenger => cfg.PageId,
        ChannelType.Instagram         => cfg.InstagramAccountId,
        _                             => null,
    };

    /// <summary>Ruta relativa del webhook que Meta debe llamar para este canal (ver WebhooksController).</summary>
    public static string WebhookPath(ChannelType channel, int businessId) => channel == ChannelType.WhatsApp
        ? $"/api/webhooks/whatsapp/{businessId}"
        : $"/api/webhooks/meta/{businessId}";

    /// <summary>Deja solo los dígitos de un teléfono o de un wid ("51987654321@c.us").</summary>
    public static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var at = raw.IndexOf('@');
        var digits = new string((at >= 0 ? raw[..at] : raw).Where(char.IsAsciiDigit).ToArray());
        return digits.Length is >= 6 and <= 20 ? digits : null;
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Pista de un secreto cifrado. Nunca devuelve el valor: solo si existe y sus últimos 4 caracteres
    /// (únicamente si es lo bastante largo como para no revelar una parte significativa).
    /// <paramref name="readable"/> es false si el cifrado no se puede leer (p.ej. cambió el anillo de claves).
    /// </summary>
    public static ChannelSecretHintDto SecretHint(DataProtectionService dp, string? cipher, out bool readable)
    {
        readable = true;
        if (string.IsNullOrWhiteSpace(cipher)) return new ChannelSecretHintDto(false, null);
        var plain = dp.TryDecrypt(cipher);
        if (plain == null)
        {
            readable = false;
            return new ChannelSecretHintDto(true, null);
        }
        return new ChannelSecretHintDto(true, plain.Length >= 12 ? plain[^4..] : null);
    }

    public static ChannelServiceStatusDto ServiceStatus(WaClientSupervisor supervisor, bool healthy)
        => new(supervisor.IsManaged, healthy, supervisor.IsRestarting, supervisor.ConsecutiveFailures);

    /// <summary>Resumen visible de un canal. <paramref name="session"/> solo aplica a WhatsApp QR activo.</summary>
    public static ChannelSummaryDto BuildSummary(ChannelType channel, ChannelConfig? cfg, WaSessionStatus? session,
        DataProtectionService dp, string baseUrl, int businessId, bool includeVerifyToken)
    {
        var accessToken = SecretHint(dp, cfg?.AccessTokenEncrypted, out var tokenReadable);
        var appSecret = SecretHint(dp, cfg?.AppSecretEncrypted, out var secretReadable);
        var webhookUrl = baseUrl + WebhookPath(channel, businessId);

        ChannelWhatsAppMode? mode = channel != ChannelType.WhatsApp ? null
            : cfg != null && IsWhatsAppCloud(cfg) ? ChannelWhatsAppMode.CloudApi : ChannelWhatsAppMode.Qr;

        var (status, detail) = cfg is not { IsActive: true }
            ? (ChannelConnectionStatus.Disconnected, (string?)null)
            : mode == ChannelWhatsAppMode.Qr
                ? QrStatus(session)
                : MetaStatus(cfg, accessToken, tokenReadable, appSecret, secretReadable);

        return new ChannelSummaryDto(
            channel,
            Configured: cfg != null,
            IsActive: cfg?.IsActive == true,
            status,
            detail,
            mode,
            SessionState: mode == ChannelWhatsAppMode.Qr && cfg?.IsActive == true ? session?.State ?? OfflineState : null,
            cfg?.PhoneNumber,
            cfg?.PhoneNumberId,
            cfg?.WabaId,
            cfg?.PageId,
            cfg?.InstagramAccountId,
            cfg?.IsActive == true ? cfg.ConnectedAt : null,
            accessToken,
            appSecret,
            webhookUrl,
            includeVerifyToken ? cfg?.WebhookVerifyToken : null);
    }

    private static (ChannelConnectionStatus, string?) QrStatus(WaSessionStatus? session) => session?.State switch
    {
        _ when session?.Connected == true => (ChannelConnectionStatus.Connected, null),
        null or OfflineState => (ChannelConnectionStatus.NeedsAttention,
            "El servicio de WhatsApp no responde. Espera unos segundos o reinícialo desde la página de WhatsApp."),
        "initializing" or "authenticating" => (ChannelConnectionStatus.NeedsAttention,
            "Restableciendo la sesión de WhatsApp. Puede tardar hasta un minuto."),
        _ => (ChannelConnectionStatus.NeedsAttention,
            "La sesión de WhatsApp se cerró desde el celular. Vuelve a escanear el código QR."),
    };

    private static (ChannelConnectionStatus, string?) MetaStatus(ChannelConfig cfg, ChannelSecretHintDto token, bool tokenReadable,
        ChannelSecretHintDto secret, bool secretReadable)
    {
        if (string.IsNullOrWhiteSpace(MetaAccountId(cfg)))
            return (ChannelConnectionStatus.NeedsAttention, "Falta el identificador de la cuenta de Meta.");
        if (!token.Configured || !tokenReadable)
            return (ChannelConnectionStatus.NeedsAttention, "No se pudo leer el token de acceso guardado. Vuelve a ingresarlo.");
        if (!secret.Configured || !secretReadable)
            return (ChannelConnectionStatus.NeedsAttention,
                "Falta la clave secreta de la app: sin ella los mensajes de Meta se rechazan por seguridad.");
        return (ChannelConnectionStatus.Connected, null);
    }
}
