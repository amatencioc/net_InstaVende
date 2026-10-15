using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.SellerData;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Zona de entrega. <c>Cost</c> null = costo a coordinar; 0 = envío gratis.</summary>
public sealed record DeliveryZoneDto(int Id, string Name, decimal? Cost, string? Description, bool IsActive, int SortOrder);

public sealed record DeliveryZoneUpsertRequest(
    [Required(ErrorMessage = "El nombre es obligatorio."),
               StringLength(100, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 100 caracteres.")]
    string Name,
    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "El costo debe ser 0 o mayor.")]
    decimal? Cost,
    [StringLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
    string? Description,
    bool IsActive = true);

public sealed record DeliveryZoneToggleRequest(bool IsActive);

public sealed record DeliveryZoneOrderRequest(
    [Required(ErrorMessage = "Envía el nuevo orden.")] IReadOnlyList<int> Ids);

/// <summary>Zonas y costos de entrega que el vendedor IA informa a los clientes.</summary>
[Route("api/delivery-zones")]
public sealed class DeliveryZonesController(AppDbContext db) : TenantController
{
    private const string NotFoundMessage = "La zona de entrega no existe o fue eliminada.";

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DeliveryZoneDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeliveryZoneDto>>> List(CancellationToken ct) =>
        await db.DeliveryZones.AsNoTracking()
            .Where(z => z.BusinessId == BusinessId)
            .OrderBy(z => z.SortOrder).ThenBy(z => z.Id)
            .Select(z => new DeliveryZoneDto(z.Id, z.Name, z.Cost, z.Description, z.IsActive, z.SortOrder))
            .ToListAsync(ct);

    [HttpPost]
    [ProducesResponseType<DeliveryZoneDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeliveryZoneDto>> Create(DeliveryZoneUpsertRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await NameTakenAsync(name, exceptId: null, ct)) return DuplicateName(name);

        var nextOrder = await db.DeliveryZones.Where(z => z.BusinessId == BusinessId)
            .MaxAsync(z => (int?)z.SortOrder, ct) ?? -1;
        var zone = new DeliveryZone
        {
            BusinessId = BusinessId,
            Name = name,
            Cost = RoundCost(request.Cost),
            Description = SellerDataText.Clean(request.Description),
            IsActive = request.IsActive,
            SortOrder = nextOrder + 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.DeliveryZones.Add(zone);
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, ToDto(zone));
    }

    /// <summary>Edita una zona. Conserva su posición (SortOrder) en la lista.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<DeliveryZoneDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeliveryZoneDto>> Update(int id, DeliveryZoneUpsertRequest request, CancellationToken ct)
    {
        var zone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == id && z.BusinessId == BusinessId, ct);
        if (zone == null) return NotFoundProblem(NotFoundMessage);

        var name = request.Name.Trim();
        if (await NameTakenAsync(name, exceptId: id, ct)) return DuplicateName(name);

        zone.Name = name;
        zone.Cost = RoundCost(request.Cost);
        zone.Description = SellerDataText.Clean(request.Description);
        zone.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return ToDto(zone);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var zone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == id && z.BusinessId == BusinessId, ct);
        if (zone == null) return NotFoundProblem(NotFoundMessage);
        db.DeliveryZones.Remove(zone);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Activa o desactiva una zona (el bot solo ofrece las activas). Se envía el estado deseado: es idempotente.</summary>
    [HttpPost("{id:int}/toggle")]
    [ProducesResponseType<DeliveryZoneDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeliveryZoneDto>> Toggle(int id, DeliveryZoneToggleRequest request, CancellationToken ct)
    {
        var zone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == id && z.BusinessId == BusinessId, ct);
        if (zone == null) return NotFoundProblem(NotFoundMessage);
        if (zone.IsActive != request.IsActive)
        {
            zone.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
        }
        return ToDto(zone);
    }

    /// <summary>Reordena las zonas: <c>ids</c> en el nuevo orden (las no enviadas quedan al final).</summary>
    [HttpPut("order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder(DeliveryZoneOrderRequest request, CancellationToken ct)
    {
        var zones = await db.DeliveryZones.Where(z => z.BusinessId == BusinessId)
            .OrderBy(z => z.SortOrder).ThenBy(z => z.Id)
            .ToListAsync(ct);

        var (order, error) = SellerDataText.ResolveOrder(request.Ids, zones.Select(z => z.Id).ToList());
        if (error != null) return BadRequestProblem(error);

        var byId = zones.ToDictionary(z => z.Id);
        for (var i = 0; i < order!.Count; i++) byId[order[i]].SortOrder = i;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        return db.DeliveryZones.AnyAsync(z => z.BusinessId == BusinessId && z.Id != exceptId && z.Name.Trim().ToLower() == lower, ct);
    }

    private ObjectResult DuplicateName(string name) => ConflictProblem($"Ya tienes una zona de entrega llamada \"{name}\".");

    private static decimal? RoundCost(decimal? cost) => cost is { } c ? Math.Round(c, 2, MidpointRounding.AwayFromZero) : null;

    private static DeliveryZoneDto ToDto(DeliveryZone z) => new(z.Id, z.Name, z.Cost, z.Description, z.IsActive, z.SortOrder);
}
