using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Inbox;

/// <summary>Etiquetas para organizar las conversaciones de la bandeja.</summary>
[Route("api/conversation-labels")]
public sealed class ConversationLabelsController(AppDbContext db) : TenantController
{
    private const int MaxLabelsPerBusiness = 50;

    /// <summary>Etiquetas del negocio ordenadas por nombre, con el número de conversaciones de cada una.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ConversationLabelDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationLabelDto>>> List(CancellationToken ct)
    {
        var bid = BusinessId;
        return await db.ConversationLabels.AsNoTracking()
            .Where(l => l.BusinessId == bid)
            .OrderBy(l => l.Name)
            .Select(l => new ConversationLabelDto(l.Id, l.Name, l.Color, l.Conversations.Count()))
            .ToListAsync(ct);
    }

    [HttpPost]
    [ProducesResponseType<ConversationLabelDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConversationLabelDto>> Create(ConversationLabelUpsertRequest request, CancellationToken ct)
    {
        var bid = BusinessId;
        var name = request.Name.Trim();
        if (name.Length == 0) return BadRequestProblem("El nombre es obligatorio.");

        if (await db.ConversationLabels.CountAsync(l => l.BusinessId == bid, ct) >= MaxLabelsPerBusiness)
            return ConflictProblem($"Puedes tener como máximo {MaxLabelsPerBusiness} etiquetas.");
        if (await NameTakenAsync(bid, name, exceptId: null, ct))
            return ConflictProblem("Ya existe una etiqueta con ese nombre.");

        var label = new ConversationLabel { BusinessId = bid, Name = name, Color = request.Color.ToLowerInvariant() };
        db.ConversationLabels.Add(label);
        await db.SaveChangesAsync(ct);

        return Created($"/api/conversation-labels/{label.Id}", new ConversationLabelDto(label.Id, label.Name, label.Color, 0));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ConversationLabelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConversationLabelDto>> Update(int id, ConversationLabelUpsertRequest request, CancellationToken ct)
    {
        var bid = BusinessId;
        var name = request.Name.Trim();
        if (name.Length == 0) return BadRequestProblem("El nombre es obligatorio.");

        var label = await db.ConversationLabels.FirstOrDefaultAsync(l => l.Id == id && l.BusinessId == bid, ct);
        if (label is null) return NotFoundProblem("La etiqueta no existe o fue eliminada.");
        if (await NameTakenAsync(bid, name, exceptId: id, ct))
            return ConflictProblem("Ya existe una etiqueta con ese nombre.");

        label.Name = name;
        label.Color = request.Color.ToLowerInvariant();
        await db.SaveChangesAsync(ct);

        var count = await db.Conversations.CountAsync(c => c.BusinessId == bid && c.LabelId == id, ct);
        return new ConversationLabelDto(label.Id, label.Name, label.Color, count);
    }

    /// <summary>Elimina la etiqueta y la quita de las conversaciones que la tenían.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var bid = BusinessId;
        if (!await db.ConversationLabels.AnyAsync(l => l.Id == id && l.BusinessId == bid, ct))
            return NotFoundProblem("La etiqueta no existe o fue eliminada.");

        // La FK Conversation → Label es NoAction: se desvinculan las conversaciones en la misma transacción
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Conversations
                .Where(c => c.BusinessId == bid && c.LabelId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LabelId, (int?)null), ct);
            await db.ConversationLabels
                .Where(l => l.Id == id && l.BusinessId == bid)
                .ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
        });
        return NoContent();
    }

    private Task<bool> NameTakenAsync(int businessId, string name, int? exceptId, CancellationToken ct)
        => db.ConversationLabels.AnyAsync(l => l.BusinessId == businessId && l.Name == name && l.Id != exceptId, ct);
}
