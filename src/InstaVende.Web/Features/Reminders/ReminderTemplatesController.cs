using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Reminders;

/// <summary>
/// Recordatorios automáticos por segmento de cliente (Frío, Tibio, Caliente) con ventanas
/// de envío de 2-3h y 22-23h. Si el negocio no guardó un segmento se devuelven los
/// mensajes por defecto sin escribir en la base de datos.
/// </summary>
[Route("api/reminder-templates")]
public sealed class ReminderTemplatesController(AppDbContext db) : TenantController
{
    /// <summary>Los tres segmentos con sus recordatorios (guardados o por defecto).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReminderTemplateSegmentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReminderTemplateSegmentDto>>> List(CancellationToken ct)
    {
        var stored = await db.ReminderTemplates.AsNoTracking()
            .Where(t => t.BusinessId == BusinessId)
            .Select(t => new StoredTemplate(t.Segment, t.Order, t.Message, t.IsActive, t.UpdatedAt))
            .ToListAsync(ct);

        return ReminderTemplateCatalog.Segments
            .Select(segment => BuildSegment(segment, stored.Where(t => t.Segment == segment).ToList()))
            .ToList();
    }

    /// <summary>Reemplaza los recordatorios de un segmento.</summary>
    [HttpPut("{segment}")]
    [RequireBusinessAdmin]
    [ProducesResponseType<ReminderTemplateSegmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReminderTemplateSegmentDto>> Update(
        CustomerSegment segment, ReminderTemplateSegmentUpdateRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(segment)) return BadRequestProblem("El segmento indicado no existe.");

        var max = ReminderTemplateCatalog.MaxReminders(segment);
        var items = request.Reminders;
        if (items.Any(i => i.Order > max))
            ModelState.AddModelError(nameof(request.Reminders), max == 1
                ? "El segmento Frío solo admite un recordatorio."
                : $"Este segmento admite como máximo {max} recordatorios.");
        if (items.Select(i => i.Order).Distinct().Count() != items.Count)
            ModelState.AddModelError(nameof(request.Reminders), "Hay recordatorios con el mismo orden.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // Una consulta para el conjunto actual del segmento y un único SaveChanges.
        var existing = await db.ReminderTemplates
            .Where(t => t.BusinessId == BusinessId && t.Segment == segment)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var byOrder = items.ToDictionary(i => i.Order);
        var kept = new HashSet<int>();

        foreach (var template in existing.OrderBy(t => t.Id))
        {
            // Se eliminan las posiciones que ya no llegan y los duplicados históricos de una misma posición.
            if (!byOrder.TryGetValue(template.Order, out var item) || !kept.Add(template.Order))
            {
                db.ReminderTemplates.Remove(template);
                continue;
            }
            template.Message    = item.Message.Trim();
            template.IsActive   = item.IsActive;
            template.TimeWindow = ReminderTemplateCatalog.WindowFor(item.Order);
            template.UpdatedAt  = now;
        }

        foreach (var item in items.Where(i => !kept.Contains(i.Order)))
        {
            db.ReminderTemplates.Add(new ReminderTemplate
            {
                BusinessId = BusinessId,
                Segment    = segment,
                Order      = item.Order,
                Message    = item.Message.Trim(),
                TimeWindow = ReminderTemplateCatalog.WindowFor(item.Order),
                IsActive   = item.IsActive,
                CreatedAt  = now,
                UpdatedAt  = now,
            });
        }

        await db.SaveChangesAsync(ct);

        var saved = items
            .Select(i => new StoredTemplate(segment, i.Order, i.Message.Trim(), i.IsActive, now))
            .ToList();
        return BuildSegment(segment, saved);
    }

    /// <summary>Mensajes sugeridos para un segmento (catálogo estático).</summary>
    [HttpGet("suggestions")]
    [ProducesResponseType<IReadOnlyList<ReminderTemplateSuggestionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<ReminderTemplateSuggestionDto>> Suggestions([FromQuery] CustomerSegment segment)
    {
        if (!Enum.IsDefined(segment)) return BadRequestProblem("El segmento indicado no existe.");
        return Ok(ReminderTemplateCatalog.Suggestions(segment));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed record StoredTemplate(CustomerSegment Segment, int Order, string Message, bool IsActive, DateTime UpdatedAt);

    /// <summary>
    /// Combina lo guardado con los valores por defecto: cada posición admitida por el segmento
    /// aparece una vez (si falta se usa la sugerida). Posiciones fuera de rango se ignoran.
    /// </summary>
    private static ReminderTemplateSegmentDto BuildSegment(CustomerSegment segment, IReadOnlyList<StoredTemplate> stored)
    {
        var max = ReminderTemplateCatalog.MaxReminders(segment);
        var defaults = ReminderTemplateCatalog.Defaults(segment);
        var valid = stored.Where(t => t.Order >= 1 && t.Order <= max).ToList();

        var reminders = defaults
            .Select(d => valid.Where(t => t.Order == d.Order).Select(t =>
                    new ReminderTemplateItemDto(t.Order, ReminderTemplateCatalog.WindowFor(t.Order), t.Message, t.IsActive))
                .FirstOrDefault() ?? d)
            .ToList();

        return new ReminderTemplateSegmentDto(
            segment,
            max,
            IsDefault: valid.Count == 0,
            reminders,
            UpdatedAt: valid.Count == 0 ? null : valid.Max(t => t.UpdatedAt));
    }
}
