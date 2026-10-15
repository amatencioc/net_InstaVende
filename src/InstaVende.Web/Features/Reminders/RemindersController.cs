using System.Linq.Expressions;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using InstaVende.Web.Features.Shared;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Reminders;

/// <summary>
/// Recordatorios programados manualmente por los agentes. El envío lo realiza
/// <c>ReminderDispatcher</c> cuando vence <c>ScheduledAt</c> (UTC).
/// </summary>
[Route("api/reminders")]
public sealed class RemindersController(AppDbContext db, CurrentUserService currentUser) : TenantController
{
    private static readonly TimeSpan MaxScheduleAhead = TimeSpan.FromDays(90);
    private const int ContactSearchLimit = 20;

    /// <summary>Listado paginado de recordatorios del negocio.</summary>
    /// <param name="status">Filtra por estado. Sin valor devuelve todos.</param>
    /// <param name="from">Primer día (fecha local del negocio, inclusive) de la fecha programada.</param>
    /// <param name="to">Último día (fecha local del negocio, inclusive) de la fecha programada.</param>
    /// <param name="search">Busca en nombre/teléfono del contacto y en el mensaje.</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<ReminderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReminderDto>>> List(
        [FromQuery] ReminderStatus? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var query = Filtered(from, to, search);
        if (status is { } s) query = query.Where(r => r.Status == s);

        // Pendientes: el próximo envío primero. Resto (historial): lo más reciente primero.
        query = status == ReminderStatus.Pending
            ? query.OrderBy(r => r.ScheduledAt).ThenBy(r => r.Id)
            : query.OrderByDescending(r => r.ScheduledAt).ThenByDescending(r => r.Id);

        return await query.Select(ToDto).ToPagedResultAsync(PageRequest.From(page, pageSize), ct);
    }

    /// <summary>Cantidad de recordatorios por estado con los mismos filtros del listado (sin estado).</summary>
    [HttpGet("counts")]
    [ProducesResponseType<ReminderCountsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReminderCountsDto>> Counts(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search, CancellationToken ct)
    {
        var counts = await Filtered(from, to, search)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Of(ReminderStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;
        return new ReminderCountsDto(Of(ReminderStatus.Pending), Of(ReminderStatus.Sent),
            Of(ReminderStatus.Failed), Of(ReminderStatus.Cancelled));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderDto>> Get(int id, CancellationToken ct)
    {
        var dto = await db.Reminders.AsNoTracking()
            .Where(r => r.Id == id && r.BusinessId == BusinessId)
            .Select(ToDto)
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFoundProblem("El recordatorio no existe o fue eliminado.") : dto;
    }

    /// <summary>Programa un recordatorio para un contacto del negocio.</summary>
    [HttpPost]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReminderDto>> Create(ReminderUpsertRequest request, CancellationToken ct)
    {
        var scheduledAt = NormalizeToUtc(request.ScheduledAt);
        if (ValidateSchedule(scheduledAt) is { } scheduleError) return scheduleError;

        var target = await ResolveTargetAsync(request.ContactId, request.ConversationId, ct);
        if (target.Error is not null) return target.Error;

        var reminder = new Reminder
        {
            BusinessId       = BusinessId,
            ContactId        = request.ContactId,
            ConversationId   = request.ConversationId,
            ChannelType      = target.Channel,
            Message          = request.Message.Trim(),
            ScheduledAt      = scheduledAt,
            TemplateKey      = NullIfEmpty(request.TemplateKey),
            CreatedByAgentId = currentUser.GetUserId(),
            Status           = ReminderStatus.Pending,
        };
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(ct);

        var dto = await db.Reminders.AsNoTracking().Where(r => r.Id == reminder.Id).Select(ToDto).FirstAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = reminder.Id }, dto);
    }

    /// <summary>Edita un recordatorio pendiente.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReminderDto>> Update(int id, ReminderUpsertRequest request, CancellationToken ct)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.BusinessId == BusinessId, ct);
        if (reminder is null) return NotFoundProblem("El recordatorio no existe o fue eliminado.");
        if (reminder.Status != ReminderStatus.Pending)
            return ConflictProblem("Solo puedes editar recordatorios pendientes.");

        var scheduledAt = NormalizeToUtc(request.ScheduledAt);
        if (ValidateSchedule(scheduledAt) is { } scheduleError) return scheduleError;

        var contactChanged = reminder.ContactId != request.ContactId;
        // Si no se envía conversación se conserva la actual, salvo que cambie el contacto (ya no le correspondería).
        var conversationId = request.ConversationId ?? (contactChanged ? null : reminder.ConversationId);

        var target = await ResolveTargetAsync(request.ContactId, conversationId, ct);
        if (target.Error is not null) return target.Error;

        reminder.ContactId      = request.ContactId;
        reminder.ConversationId = conversationId;
        reminder.ChannelType    = target.Channel;
        reminder.Message        = request.Message.Trim();
        reminder.ScheduledAt    = scheduledAt;
        reminder.TemplateKey    = NullIfEmpty(request.TemplateKey) ?? reminder.TemplateKey;
        await db.SaveChangesAsync(ct);

        return await db.Reminders.AsNoTracking().Where(r => r.Id == id).Select(ToDto).FirstAsync(ct);
    }

    /// <summary>Cancela un recordatorio pendiente (no se enviará).</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        // Actualización condicional atómica: no pisa un recordatorio que el despachador acaba de procesar.
        var updated = await db.Reminders
            .Where(r => r.Id == id && r.BusinessId == BusinessId && r.Status == ReminderStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReminderStatus.Cancelled), ct);
        if (updated > 0) return NoContent();

        var exists = await db.Reminders.AnyAsync(r => r.Id == id && r.BusinessId == BusinessId, ct);
        return exists
            ? ConflictProblem("Solo puedes cancelar recordatorios pendientes.")
            : NotFoundProblem("El recordatorio no existe o fue eliminado.");
    }

    /// <summary>Elimina un recordatorio que no haya sido enviado.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await db.Reminders
            .Where(r => r.Id == id && r.BusinessId == BusinessId && r.Status != ReminderStatus.Sent)
            .ExecuteDeleteAsync(ct);
        if (deleted > 0) return NoContent();

        var exists = await db.Reminders.AnyAsync(r => r.Id == id && r.BusinessId == BusinessId, ct);
        return exists
            ? ConflictProblem("No puedes eliminar un recordatorio que ya fue enviado: forma parte del historial.")
            : NotFoundProblem("El recordatorio no existe o fue eliminado.");
    }

    /// <summary>Busca contactos del negocio para el selector (máximo 20, los más recientes primero).</summary>
    [HttpGet("contacts")]
    [ProducesResponseType<IReadOnlyList<ReminderContactOptionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReminderContactOptionDto>>> SearchContacts(
        [FromQuery] string? search, CancellationToken ct)
    {
        var query = db.Contacts.AsNoTracking().Where(c => c.BusinessId == BusinessId);
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(c => (c.Name != null && c.Name.Contains(term)) || (c.Phone != null && c.Phone.Contains(term)));

        return await query
            .OrderByDescending(c => c.LastSeenAt)
            .Take(ContactSearchLimit)
            .Select(c => new ReminderContactOptionDto(c.Id, c.Name, c.Phone, c.AvatarUrl, c.ChannelType, c.LastSeenAt))
            .ToListAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly Expression<Func<Reminder, ReminderDto>> ToDto = r => new ReminderDto(
        r.Id,
        r.ContactId,
        r.Contact.Name,
        r.Contact.Phone,
        r.Contact.AvatarUrl,
        r.Contact.ChannelType,
        r.ConversationId,
        r.Message,
        r.ScheduledAt,
        r.Status,
        r.SentAt,
        r.TemplateKey,
        r.CreatedByAgent != null ? (r.CreatedByAgent.FirstName + " " + r.CreatedByAgent.LastName).Trim() : null,
        r.CreatedAt);

    /// <summary>Consulta base del negocio con los filtros comunes de listado y contadores.</summary>
    private IQueryable<Reminder> Filtered(DateOnly? from, DateOnly? to, string? search)
    {
        var query = db.Reminders.AsNoTracking().Where(r => r.BusinessId == BusinessId);

        // Los días se interpretan en la zona horaria del negocio.
        if (from is { } f)
        {
            var fromUtc = Tenant.ToUtc(f.ToDateTime(TimeOnly.MinValue));
            query = query.Where(r => r.ScheduledAt >= fromUtc);
        }
        if (to is { } t)
        {
            var toUtc = Tenant.ToUtc(t.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(r => r.ScheduledAt < toUtc);
        }

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(r =>
                (r.Contact.Name != null && r.Contact.Name.Contains(term)) ||
                (r.Contact.Phone != null && r.Contact.Phone.Contains(term)) ||
                r.Message.Contains(term));

        return query;
    }

    /// <summary>
    /// UTC tal cual; con desplazamiento → a UTC; sin zona horaria → hora "de pared" del negocio.
    /// </summary>
    private DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc   => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _                  => Tenant.ToUtc(value),
    };

    private ActionResult? ValidateSchedule(DateTime scheduledAtUtc)
    {
        var now = DateTime.UtcNow;
        if (scheduledAtUtc <= now)
            return ScheduleError("La fecha de envío debe ser posterior a la hora actual.");
        if (scheduledAtUtc > now.Add(MaxScheduleAhead))
            return ScheduleError("Solo puedes programar recordatorios hasta 90 días en el futuro.");
        return null;
    }

    private ActionResult ScheduleError(string message)
    {
        ModelState.AddModelError(nameof(ReminderUpsertRequest.ScheduledAt), message);
        return ValidationProblem(ModelState);
    }

    private readonly record struct ReminderTarget(ChannelType Channel, ActionResult? Error);

    /// <summary>Valida que el contacto (y la conversación, si llega) pertenezcan al negocio y entre sí, en una sola consulta.</summary>
    private async Task<ReminderTarget> ResolveTargetAsync(int contactId, int? conversationId, CancellationToken ct)
    {
        var contact = await db.Contacts.AsNoTracking()
            .Where(c => c.Id == contactId && c.BusinessId == BusinessId)
            .Select(c => new
            {
                c.ChannelType,
                ConversationOk = conversationId == null ||
                    c.Conversations.Any(cv => cv.Id == conversationId && cv.BusinessId == BusinessId),
            })
            .FirstOrDefaultAsync(ct);

        if (contact is null)
        {
            ModelState.AddModelError(nameof(ReminderUpsertRequest.ContactId), "El contacto no existe en tu negocio.");
            return new ReminderTarget(default, ValidationProblem(ModelState));
        }
        if (!contact.ConversationOk)
        {
            ModelState.AddModelError(nameof(ReminderUpsertRequest.ConversationId), "La conversación no pertenece a este contacto.");
            return new ReminderTarget(default, ValidationProblem(ModelState));
        }
        return new ReminderTarget(contact.ChannelType, null);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
