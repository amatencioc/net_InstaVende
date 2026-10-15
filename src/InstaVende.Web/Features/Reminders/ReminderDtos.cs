using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;

namespace InstaVende.Web.Features.Reminders;

// ── Recordatorios programados ────────────────────────────────────────────────

/// <summary>Recordatorio programado tal como se muestra en el listado.</summary>
/// <param name="Channel">Canal del contacto (por el que se enviará).</param>
/// <param name="ScheduledAt">Fecha de envío programada (UTC).</param>
/// <param name="SentAt">Fecha en que el despachador lo procesó (UTC), si ya ocurrió.</param>
/// <param name="CreatedByName">Nombre del agente que lo creó (null si lo programó el sistema).</param>
public sealed record ReminderDto(
    int Id,
    int ContactId,
    string? ContactName,
    string? ContactPhone,
    string? ContactAvatarUrl,
    ChannelType Channel,
    int? ConversationId,
    string Message,
    DateTime ScheduledAt,
    ReminderStatus Status,
    DateTime? SentAt,
    string? TemplateKey,
    string? CreatedByName,
    DateTime CreatedAt);

/// <summary>Cantidad de recordatorios por estado (para las pestañas del listado).</summary>
public sealed record ReminderCountsDto(int Pending, int Sent, int Failed, int Cancelled);

/// <summary>Datos para crear o editar un recordatorio.</summary>
/// <param name="ContactId">Contacto del negocio que recibirá el mensaje.</param>
/// <param name="ConversationId">Conversación concreta (opcional; debe ser del mismo contacto). Si se omite al editar, se conserva la actual.</param>
/// <param name="Message">Texto a enviar (1-1000 caracteres).</param>
/// <param name="ScheduledAt">
/// Fecha de envío en UTC (ISO-8601 con "Z"). Debe ser futura y como máximo a 90 días.
/// Si llega sin zona horaria se interpreta como hora local del negocio.
/// </param>
/// <param name="TemplateKey">Identificador opcional de la plantilla de origen. Si se omite al editar, se conserva el actual.</param>
public sealed record ReminderUpsertRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un contacto.")] int ContactId,
    int? ConversationId,
    [Required(ErrorMessage = "Escribe el mensaje del recordatorio."),
               StringLength(1000, MinimumLength = 1, ErrorMessage = "El mensaje debe tener entre 1 y 1000 caracteres.")]
    string Message,
    [Required(ErrorMessage = "Indica la fecha y hora de envío.")] DateTime ScheduledAt,
    [StringLength(100, ErrorMessage = "La clave de plantilla no puede superar los 100 caracteres.")] string? TemplateKey = null);

/// <summary>Contacto del negocio para el selector del recordatorio.</summary>
public sealed record ReminderContactOptionDto(
    int Id,
    string? Name,
    string? Phone,
    string? AvatarUrl,
    ChannelType Channel,
    DateTime LastSeenAt);

// ── Plantillas por segmento ─────────────────────────────────────────────────

/// <summary>Recordatorios automáticos configurados para un segmento de cliente.</summary>
/// <param name="MaxReminders">Recordatorios que admite el segmento (Frío: 1; Tibio y Caliente: 2).</param>
/// <param name="IsDefault">true si el negocio aún no guardó este segmento y se devuelven los valores sugeridos.</param>
public sealed record ReminderTemplateSegmentDto(
    CustomerSegment Segment,
    int MaxReminders,
    bool IsDefault,
    IReadOnlyList<ReminderTemplateItemDto> Reminders,
    DateTime? UpdatedAt);

/// <param name="Order">Posición del recordatorio (1 o 2).</param>
/// <param name="TimeWindow">Ventana de envío tras el último mensaje del cliente ("2-3h" o "22-23h").</param>
public sealed record ReminderTemplateItemDto(int Order, string TimeWindow, string Message, bool IsActive);

/// <summary>Nuevo conjunto de recordatorios de un segmento (reemplaza el actual).</summary>
public sealed record ReminderTemplateSegmentUpdateRequest(
    [Required(ErrorMessage = "Envía los recordatorios del segmento."),
               MinLength(1, ErrorMessage = "Envía al menos un recordatorio."),
               MaxLength(2, ErrorMessage = "Un segmento admite como máximo 2 recordatorios.")]
    IReadOnlyList<ReminderTemplateItemUpdate> Reminders);

public sealed record ReminderTemplateItemUpdate(
    [Range(1, 2, ErrorMessage = "El orden del recordatorio debe ser 1 o 2.")] int Order,
    [Required(ErrorMessage = "Escribe el mensaje del recordatorio."),
               StringLength(ReminderTemplateCatalog.MaxMessageLength, MinimumLength = 1,
                   ErrorMessage = "El mensaje debe tener entre 1 y 200 caracteres.")]
    string Message,
    bool IsActive);

/// <summary>Mensaje sugerido para un recordatorio de segmento.</summary>
/// <param name="Order">Recordatorio para el que está pensado (1 = 2-3h, 2 = 22-23h).</param>
public sealed record ReminderTemplateSuggestionDto(string Id, string Title, string Message, int Order);
