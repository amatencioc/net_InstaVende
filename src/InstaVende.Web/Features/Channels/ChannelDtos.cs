using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;

namespace InstaVende.Web.Features.Channels;

// ── Resumen de canales ───────────────────────────────────────────────────────

/// <summary>Estado de conexión de un canal tal como lo ve el comercio.</summary>
public enum ChannelConnectionStatus
{
    // Activo y funcionando.
    Connected,
    // Sin configurar o desactivado.
    Disconnected,
    // Activo pero con un problema que impide recibir o enviar mensajes.
    NeedsAttention,
}

/// <summary>Forma de conexión de WhatsApp.</summary>
public enum ChannelWhatsAppMode
{
    // Sesión de WhatsApp Web vinculada con código QR (wa-client).
    Qr,
    // API oficial de WhatsApp Business (Cloud API de Meta).
    CloudApi,
}

/// <summary>Secreto guardado: nunca se devuelve su valor, solo si existe y sus últimos 4 caracteres.</summary>
public sealed record ChannelSecretHintDto(bool Configured, string? Last4);

/// <summary>Estado del proceso que mantiene las sesiones QR de WhatsApp.</summary>
public sealed record ChannelServiceStatusDto(
    // true si la API lanza y supervisa el proceso (se puede reiniciar desde el panel).
    bool Managed,
    bool Healthy,
    bool Restarting,
    int ConsecutiveFailures);

public sealed record ChannelSummaryDto(
    ChannelType Channel,
    // true si existe configuración guardada para el canal.
    bool Configured,
    bool IsActive,
    ChannelConnectionStatus Status,
    // Motivo legible cuando el estado es <see cref="ChannelConnectionStatus.NeedsAttention"/>.
    string? StatusDetail,
    // Solo para WhatsApp.
    ChannelWhatsAppMode? WhatsAppMode,
    // Estado de la sesión QR (solo WhatsApp en modo QR activo): initializing, qr, authenticating, connected, disconnected, offline.
    string? SessionState,
    // Teléfono (sin "+") o cuenta visible del canal.
    string? PhoneNumber,
    string? PhoneNumberId,
    string? WabaId,
    string? PageId,
    string? InstagramAccountId,
    DateTime? ConnectedAt,
    ChannelSecretHintDto AccessToken,
    ChannelSecretHintDto AppSecret,
    // URL absoluta que se pega en Meta for Developers como "URL de devolución de llamada".
    string WebhookUrl,
    // Token de verificación del webhook (solo visible para administradores).
    string? WebhookVerifyToken);

public sealed record ChannelsOverviewDto(IReadOnlyList<ChannelSummaryDto> Channels, ChannelServiceStatusDto Service);

// ── Meta (WhatsApp Cloud API, Messenger, Instagram) ──────────────────────────

public sealed record ChannelMetaConfigRequest(
    [StringLength(32, ErrorMessage = "El identificador es demasiado largo."),
               RegularExpression(@"^\d+$", ErrorMessage = "El identificador del número solo contiene dígitos.")]
    string? PhoneNumberId,
    [StringLength(32, ErrorMessage = "El identificador es demasiado largo."),
               RegularExpression(@"^\d+$", ErrorMessage = "El identificador de la cuenta de WhatsApp Business solo contiene dígitos.")]
    string? WabaId,
    [StringLength(32, ErrorMessage = "El identificador es demasiado largo."),
               RegularExpression(@"^\d+$", ErrorMessage = "El identificador de la página solo contiene dígitos.")]
    string? PageId,
    [StringLength(32, ErrorMessage = "El identificador es demasiado largo."),
               RegularExpression(@"^\d+$", ErrorMessage = "El identificador de la cuenta de Instagram solo contiene dígitos.")]
    string? InstagramAccountId,
    // Vacío o null conserva el token actual.
    [StringLength(2048, ErrorMessage = "El token de acceso es demasiado largo."),
               RegularExpression(@"^\S*$", ErrorMessage = "El token de acceso no puede contener espacios.")]
    string? AccessToken,
    // Vacío o null conserva la clave secreta actual.
    [StringLength(256, ErrorMessage = "La clave secreta es demasiado larga."),
               RegularExpression(@"^\S*$", ErrorMessage = "La clave secreta no puede contener espacios.")]
    string? AppSecret,
    bool IsActive);

public sealed record ChannelTestResultDto(bool Ok, string Message, string? AccountName);

public sealed record ChannelActionResultDto(bool Ok, string Message);

// ── WhatsApp por QR ──────────────────────────────────────────────────────────

public sealed record WhatsAppSessionDto(
    // initializing, qr, authenticating, connected, disconnected u offline (servicio sin respuesta).
    string State,
    bool Connected,
    // Imagen del QR (data URL). null si no hay QR o si no cambió respecto a <c>qrHash</c>.
    string? QrDataUrl,
    DateTimeOffset? QrExpiresAt,
    // Segundos de validez que le quedan al QR según el reloj del servidor (evita desfases con el reloj del navegador).
    int? QrSecondsLeft,
    string? QrHash,
    // true si el QR es el mismo que el cliente ya tiene (no se reenvía la imagen).
    bool Unchanged,
    string? PhoneNumber,
    string? PushName,
    DateTime? ConnectedAt,
    ChannelServiceStatusDto Service);
