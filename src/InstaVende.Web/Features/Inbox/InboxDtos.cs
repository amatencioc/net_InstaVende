using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;

namespace InstaVende.Web.Features.Inbox;

// ── Listado ──────────────────────────────────────────────────────────────────

/// <summary>Categorías de la bandeja. <see cref="Unattended"/> = esperando humano o en atención con mensajes sin leer.</summary>
public enum InboxStatusFilter { All, Unattended, BotActive, WaitingHuman, HumanActive, Resolved }

public sealed record InboxContactSummaryDto(int Id, string? Name, string? Phone, string ExternalId, string? AvatarUrl);

public sealed record InboxLabelRefDto(int Id, string Name, string Color);

/// <summary>Agente humano asignado a la conversación.</summary>
public sealed record InboxAgentDto(string Id, string FirstName, string LastName);

public sealed record InboxConversationDto(
    int Id,
    InboxContactSummaryDto Contact,
    ChannelType Channel,
    ConversationStatus Status,
    int UnreadCount,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    InboxLabelRefDto? Label,
    InboxAgentDto? AssignedAgent);

/// <summary>Totales por categoría para el panel lateral de la bandeja.</summary>
public sealed record InboxCountsDto(int All, int Unattended, int BotActive, int WaitingHuman, int HumanActive, int Resolved);

// ── Detalle ──────────────────────────────────────────────────────────────────

public sealed record InboxContactOrderDto(
    int Id, string OrderNumber, OrderStatus Status, OrderSubStatus SubStatus, string StatusLabel, decimal Total, DateTime CreatedAt);

/// <summary>Ficha del contacto mostrada junto al chat.</summary>
public sealed record InboxContactDetailDto(
    int Id,
    string? Name,
    string? Phone,
    string ExternalId,
    string? AvatarUrl,
    ChannelType Channel,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    int TotalPurchases,
    decimal TotalSpent,
    DateTime? LastPurchaseAt,
    IReadOnlyList<InboxContactOrderDto> RecentOrders);

public sealed record InboxConversationDetailDto(
    int Id,
    ChannelType Channel,
    ConversationStatus Status,
    int UnreadCount,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    DateTime? LastMessageAt,
    InboxLabelRefDto? Label,
    InboxAgentDto? AssignedAgent,
    InboxContactDetailDto Contact);

/// <summary>Estado de la conversación tras un cambio (tomar control, resolver…).</summary>
public sealed record InboxConversationStateDto(int Id, ConversationStatus Status, InboxAgentDto? AssignedAgent, DateTime? ResolvedAt);

// ── Mensajes ─────────────────────────────────────────────────────────────────

public sealed record InboxMessageDto(
    int Id,
    int ConversationId,
    MessageDirection Direction,
    string Content,
    bool SentByBot,
    MessageDeliveryStatus DeliveryStatus,
    DateTime SentAt,
    string? MediaUrl,
    string? MediaType);

/// <summary>
/// Página de mensajes en orden cronológico ascendente (el más antiguo primero).
/// <see cref="HasMore"/> indica que existen mensajes anteriores: pedirlos con <c>before = Items[0].Id</c>.
/// </summary>
public sealed record InboxMessagePageDto(IReadOnlyList<InboxMessageDto> Items, bool HasMore);

// ── Peticiones ───────────────────────────────────────────────────────────────

public sealed record InboxSendMessageRequest(
    [Required(ErrorMessage = "Escribe un mensaje."),
               StringLength(4096, MinimumLength = 1, ErrorMessage = "El mensaje debe tener entre 1 y 4096 caracteres.")]
    string Text);

/// <summary>Nuevo estado: HumanActive (tomar control), BotActive (devolver al bot / reabrir) o Resolved.</summary>
public sealed record InboxChangeStatusRequest(ConversationStatus Status);

public sealed record InboxSetLabelRequest(int? LabelId);

// ── Etiquetas ────────────────────────────────────────────────────────────────

public sealed record ConversationLabelDto(int Id, string Name, string Color, int ConversationCount);

public sealed record ConversationLabelUpsertRequest(
    [Required(ErrorMessage = "El nombre es obligatorio."),
               StringLength(100, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 100 caracteres.")]
    string Name,
    [Required(ErrorMessage = "El color es obligatorio."),
               RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "El color debe tener el formato #RRGGBB.")]
    string Color);
