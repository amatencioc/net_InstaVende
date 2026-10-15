using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Settings;

/// <summary>Emails que reciben avisos del negocio (venta cerrada, conversación que necesita a un humano). Solo administradores.</summary>
[Route("api/settings/notification-emails")]
[RequireBusinessAdmin]
public sealed class NotificationEmailsController(AppDbContext db) : TenantController
{
    [HttpGet]
    [ProducesResponseType<SettingsNotificationEmailsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SettingsNotificationEmailsDto>> List(CancellationToken ct)
    {
        var items = await db.NotificationEmails.AsNoTracking()
            .Where(n => n.BusinessId == BusinessId)
            .OrderBy(n => n.Id)
            .Select(n => new SettingsNotificationEmailDto(n.Id, n.Email, n.IsActive, n.CreatedAt))
            .ToListAsync(ct);
        return new SettingsNotificationEmailsDto(items, TeamRules.MaxNotificationEmails);
    }

    [HttpPost]
    [ProducesResponseType<SettingsNotificationEmailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SettingsNotificationEmailDto>> Create(SettingsNotificationEmailCreateRequest request, CancellationToken ct)
    {
        var email = TeamRules.NormalizeEmail(request.Email);
        var existing = await db.NotificationEmails.AsNoTracking()
            .Where(n => n.BusinessId == BusinessId)
            .Select(n => n.Email)
            .ToListAsync(ct);

        if (existing.Any(e => TeamRules.NormalizeEmail(e) == email))
            return ConflictProblem("Ese email ya recibe las notificaciones.");
        if (existing.Count >= TeamRules.MaxNotificationEmails)
            return ConflictProblem($"Puedes registrar hasta {TeamRules.MaxNotificationEmails} emails de notificación.");

        var entity = new NotificationEmail { BusinessId = BusinessId, Email = email, IsActive = true };
        db.NotificationEmails.Add(entity);
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created,
            new SettingsNotificationEmailDto(entity.Id, entity.Email, entity.IsActive, entity.CreatedAt));
    }

    /// <summary>Activa o pausa un email sin borrarlo.</summary>
    [HttpPatch("{id:int}")]
    [ProducesResponseType<SettingsNotificationEmailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettingsNotificationEmailDto>> Update(int id, SettingsNotificationEmailUpdateRequest request, CancellationToken ct)
    {
        var entity = await db.NotificationEmails.FirstOrDefaultAsync(n => n.Id == id && n.BusinessId == BusinessId, ct);
        if (entity == null) return NotFoundProblem("El email no existe o fue eliminado.");
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new SettingsNotificationEmailDto(entity.Id, entity.Email, entity.IsActive, entity.CreatedAt);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await db.NotificationEmails
            .Where(n => n.Id == id && n.BusinessId == BusinessId)
            .ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFoundProblem("El email no existe o fue eliminado.") : NoContent();
    }
}
