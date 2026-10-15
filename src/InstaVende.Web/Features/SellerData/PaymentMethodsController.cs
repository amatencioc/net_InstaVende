using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.SellerData;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Método de pago que el vendedor IA ofrece a los clientes (solo los activos, en este orden).</summary>
public sealed record PaymentMethodDto(
    int Id, PaymentMethodType Type, string Name, string? Instructions, string? AccountNumber, string? AccountAlias,
    string? PaymentLink, bool IsActive, int SortOrder);

public sealed record PaymentMethodUpsertRequest(
    [property: EnumDataType(typeof(PaymentMethodType), ErrorMessage = "Selecciona un tipo de método de pago válido.")]
    PaymentMethodType Type,
    [property: Required(ErrorMessage = "El nombre es obligatorio."),
               StringLength(150, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 150 caracteres.")]
    string Name,
    [property: StringLength(1000, ErrorMessage = "Las instrucciones no pueden superar los 1000 caracteres.")]
    string? Instructions,
    [property: StringLength(100, ErrorMessage = "El número de cuenta no puede superar los 100 caracteres.")]
    string? AccountNumber,
    [property: StringLength(100, ErrorMessage = "El alias no puede superar los 100 caracteres.")]
    string? AccountAlias,
    [property: StringLength(500, ErrorMessage = "El enlace no puede superar los 500 caracteres."), HttpUrl]
    string? PaymentLink,
    bool IsActive = true);

public sealed record PaymentMethodToggleRequest(bool IsActive);

public sealed record PaymentMethodOrderRequest(
    [property: Required(ErrorMessage = "Envía el nuevo orden.")] IReadOnlyList<int> Ids);

/// <summary>Métodos de pago del negocio (fuente única: tipo <see cref="PaymentMethodType"/>, sin textos libres).</summary>
[Route("api/payment-methods")]
public sealed class PaymentMethodsController(AppDbContext db) : TenantController
{
    private const string NotFoundMessage = "El método de pago no existe o fue eliminado.";

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PaymentMethodDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodDto>>> List(CancellationToken ct) =>
        await db.PaymentMethods.AsNoTracking()
            .Where(p => p.BusinessId == BusinessId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new PaymentMethodDto(p.Id, p.Type, p.Name, p.Instructions, p.AccountNumber, p.AccountAlias,
                p.PaymentLink, p.IsActive, p.SortOrder))
            .ToListAsync(ct);

    /// <summary>Crea un método de pago y marca el paso del onboarding en la misma transacción.</summary>
    [HttpPost]
    [ProducesResponseType<PaymentMethodDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentMethodDto>> Create(PaymentMethodUpsertRequest request, CancellationToken ct)
    {
        var nextOrder = await db.PaymentMethods.Where(p => p.BusinessId == BusinessId)
            .MaxAsync(p => (int?)p.SortOrder, ct) ?? -1;
        var method = new PaymentMethod { BusinessId = BusinessId, SortOrder = nextOrder + 1, CreatedAt = DateTime.UtcNow };
        Apply(method, request);
        db.PaymentMethods.Add(method);

        var progress = await db.OnboardingProgresses.FirstOrDefaultAsync(o => o.BusinessId == BusinessId, ct);
        if (progress == null)
            db.OnboardingProgresses.Add(new OnboardingProgress { BusinessId = BusinessId, PaymentMethodsAdded = true });
        else if (!progress.PaymentMethodsAdded)
        {
            progress.PaymentMethodsAdded = true;
            progress.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, ToDto(method));
    }

    /// <summary>Edita un método de pago. Conserva su posición (SortOrder).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<PaymentMethodDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentMethodDto>> Update(int id, PaymentMethodUpsertRequest request, CancellationToken ct)
    {
        var method = await db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == BusinessId, ct);
        if (method == null) return NotFoundProblem(NotFoundMessage);
        Apply(method, request);
        await db.SaveChangesAsync(ct);
        return ToDto(method);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var method = await db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == BusinessId, ct);
        if (method == null) return NotFoundProblem(NotFoundMessage);
        db.PaymentMethods.Remove(method);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Activa o desactiva un método (el bot solo ofrece los activos). Se envía el estado deseado: es idempotente.</summary>
    [HttpPost("{id:int}/toggle")]
    [ProducesResponseType<PaymentMethodDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentMethodDto>> Toggle(int id, PaymentMethodToggleRequest request, CancellationToken ct)
    {
        var method = await db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == BusinessId, ct);
        if (method == null) return NotFoundProblem(NotFoundMessage);
        if (method.IsActive != request.IsActive)
        {
            method.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
        }
        return ToDto(method);
    }

    /// <summary>Reordena los métodos: <c>ids</c> en el nuevo orden (los no enviados quedan al final).</summary>
    [HttpPut("order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder(PaymentMethodOrderRequest request, CancellationToken ct)
    {
        var methods = await db.PaymentMethods.Where(p => p.BusinessId == BusinessId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync(ct);

        var (order, error) = SellerDataText.ResolveOrder(request.Ids, methods.Select(p => p.Id).ToList());
        if (error != null) return BadRequestProblem(error);

        var byId = methods.ToDictionary(p => p.Id);
        for (var i = 0; i < order!.Count; i++) byId[order[i]].SortOrder = i;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(PaymentMethod method, PaymentMethodUpsertRequest request)
    {
        method.Type = request.Type;
        method.Name = request.Name.Trim();
        method.Instructions = SellerDataText.Clean(request.Instructions);
        method.AccountNumber = SellerDataText.Clean(request.AccountNumber);
        method.AccountAlias = SellerDataText.Clean(request.AccountAlias);
        method.PaymentLink = SellerDataText.Clean(request.PaymentLink);
        method.IsActive = request.IsActive;
    }

    private static PaymentMethodDto ToDto(PaymentMethod p) =>
        new(p.Id, p.Type, p.Name, p.Instructions, p.AccountNumber, p.AccountAlias, p.PaymentLink, p.IsActive, p.SortOrder);
}
