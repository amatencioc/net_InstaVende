using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using InstaVende.Core.Domain;
using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Seller;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Estado del horario de atención guardado, evaluado en la hora local del negocio.</summary>
public enum SellerRulesHoursStatus { Unknown, Open, Closed }

/// <summary>Reglas operativas del vendedor IA (entidad <c>BotConfig</c>).</summary>
/// <param name="HoursStatus">Cómo interpreta el bot el horario guardado en este momento.</param>
/// <param name="UpdatedAt">Null mientras no se haya guardado nunca (se muestran valores por defecto).</param>
public sealed record SellerRulesDto(
    bool IsActive,
    string BotName,
    string FallbackMessage,
    bool EnableHandoff,
    string? HandoffTriggerPhrase,
    string? BusinessHours,
    string? ReturnPolicy,
    string? WarrantyPolicy,
    string? ShippingTimes,
    string? ActivePromotions,
    decimal DiscountLevel1,
    decimal DiscountLevel2,
    decimal DiscountLevel3,
    decimal MaxDiscountPercent,
    decimal LoyaltyDiscount,
    decimal BundleDiscount,
    decimal FreeShippingThreshold,
    string CurrencySymbol,
    SellerRulesHoursStatus HoursStatus,
    DateTime? UpdatedAt);

public sealed record SellerRulesUpdateRequest(
    bool IsActive,
    [property: Required(ErrorMessage = "El nombre interno del bot es obligatorio."), StringLength(60, ErrorMessage = "El nombre admite como máximo 60 caracteres.")]
    string BotName,
    [property: Required(ErrorMessage = "El mensaje de respaldo es obligatorio."), StringLength(500, ErrorMessage = "El mensaje de respaldo admite como máximo 500 caracteres.")]
    string FallbackMessage,
    bool EnableHandoff,
    [property: StringLength(100, ErrorMessage = "La frase disparadora admite como máximo 100 caracteres.")]
    string? HandoffTriggerPhrase,
    [property: StringLength(200, ErrorMessage = "El horario admite como máximo 200 caracteres.")]
    string? BusinessHours,
    [property: StringLength(2000, ErrorMessage = "La política de devoluciones admite como máximo 2000 caracteres.")]
    string? ReturnPolicy,
    [property: StringLength(2000, ErrorMessage = "La política de garantía admite como máximo 2000 caracteres.")]
    string? WarrantyPolicy,
    [property: StringLength(1000, ErrorMessage = "Los tiempos de envío admiten como máximo 1000 caracteres.")]
    string? ShippingTimes,
    [property: StringLength(2000, ErrorMessage = "Las promociones admiten como máximo 2000 caracteres.")]
    string? ActivePromotions,
    [property: Range(typeof(decimal), "0", "100", ErrorMessage = "El nivel 1 debe estar entre 0 y 100 %.")]
    decimal DiscountLevel1,
    [property: Range(typeof(decimal), "0", "100", ErrorMessage = "El nivel 2 debe estar entre 0 y 100 %.")]
    decimal DiscountLevel2,
    [property: Range(typeof(decimal), "0", "100", ErrorMessage = "El nivel 3 debe estar entre 0 y 100 %.")]
    decimal DiscountLevel3,
    [property: Range(typeof(decimal), "0", "100", ErrorMessage = "El descuento máximo debe estar entre 0 y 100 %.")]
    decimal MaxDiscountPercent,
    [property: Range(typeof(decimal), "0", "100", ErrorMessage = "El descuento por fidelidad debe estar entre 0 y 100 %.")]
    decimal LoyaltyDiscount,
    [property: Range(typeof(decimal), "0", "100", ErrorMessage = "El descuento por combo debe estar entre 0 y 100 %.")]
    decimal BundleDiscount,
    [property: Range(typeof(decimal), "0", "99999999", ErrorMessage = "El umbral de envío gratis no puede ser negativo.")]
    decimal FreeShippingThreshold) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DiscountLevel2 < DiscountLevel1)
            yield return new ValidationResult("El nivel 2 no puede ser menor que el nivel 1.", [nameof(DiscountLevel2)]);
        if (DiscountLevel3 < DiscountLevel2)
            yield return new ValidationResult("El nivel 3 no puede ser menor que el nivel 2.", [nameof(DiscountLevel3)]);
        if (DiscountLevel3 > MaxDiscountPercent)
            yield return new ValidationResult("Ningún nivel puede superar el descuento máximo.", [nameof(MaxDiscountPercent)]);
        if (EnableHandoff && HandoffTriggerPhrase != null && HandoffTriggerPhrase.Trim().Length is > 0 and < 3)
            yield return new ValidationResult("La frase disparadora debe tener al menos 3 caracteres.", [nameof(HandoffTriggerPhrase)]);
    }
}

/// <summary>Respuesta fija: si el mensaje del cliente contiene una de las frases, el bot responde el texto configurado sin usar IA.</summary>
public sealed record SellerRulesQuickReplyDto(
    int Id,
    string Name,
    IReadOnlyList<string> TriggerPhrases,
    string Response,
    int Priority,
    bool IsActive,
    DateTime CreatedAt);

public sealed record SellerRulesQuickReplyUpsertRequest(
    [property: Required(ErrorMessage = "El nombre es obligatorio."), StringLength(200, ErrorMessage = "El nombre admite como máximo 200 caracteres.")]
    string Name,
    [property: Required(ErrorMessage = "Agrega al menos una frase disparadora."), MaxLength(20, ErrorMessage = "Puedes agregar como máximo 20 frases.")]
    IReadOnlyList<string> TriggerPhrases,
    [property: Required(ErrorMessage = "La respuesta es obligatoria."), StringLength(1000, ErrorMessage = "La respuesta admite como máximo 1000 caracteres.")]
    string Response,
    [property: Range(0, 100, ErrorMessage = "La prioridad debe estar entre 0 y 100.")]
    int Priority,
    bool IsActive = true) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var phrases = SellerText.CleanList(TriggerPhrases, splitCommas: false);
        if (phrases.Count == 0)
            yield return new ValidationResult("Agrega al menos una frase disparadora.", [nameof(TriggerPhrases)]);
        else if (phrases.Any(p => p.Length < 3))
            yield return new ValidationResult("Cada frase debe tener al menos 3 caracteres.", [nameof(TriggerPhrases)]);
        else if (phrases.Any(p => p.Length > 100))
            yield return new ValidationResult("Cada frase admite como máximo 100 caracteres.", [nameof(TriggerPhrases)]);
    }
}

// ── Controlador ──────────────────────────────────────────────────────────────

/// <summary>Reglas operativas del vendedor: estado, horario, políticas, negociación y respuestas fijas.</summary>
[Route("api/seller")]
public sealed class SellerRulesController(AppDbContext db) : TenantController
{
    // ── Reglas (BotConfig) ───────────────────────────────────────────────────

    /// <summary>Reglas actuales. Si nunca se configuraron devuelve los valores por defecto con los que ya opera el bot.</summary>
    [HttpGet("rules")]
    [ProducesResponseType<SellerRulesDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerRulesDto>> GetRules(CancellationToken ct)
    {
        var cfg = await db.BotConfigs.AsNoTracking().FirstOrDefaultAsync(b => b.BusinessId == BusinessId, ct);
        return await ToDtoAsync(cfg ?? new BotConfig { BusinessId = BusinessId }, persisted: cfg != null, ct);
    }

    /// <summary>Guarda las reglas (crea la configuración si no existe).</summary>
    [HttpPut("rules"), RequireBusinessAdmin]
    [ProducesResponseType<SellerRulesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerRulesDto>> UpdateRules(SellerRulesUpdateRequest request, CancellationToken ct)
    {
        var cfg = await db.BotConfigs.FirstOrDefaultAsync(b => b.BusinessId == BusinessId, ct);
        if (cfg == null)
        {
            cfg = new BotConfig { BusinessId = BusinessId, CreatedAt = DateTime.UtcNow };
            db.BotConfigs.Add(cfg);
        }

        cfg.IsActive = request.IsActive;
        cfg.BotName = request.BotName.Trim();
        cfg.FallbackMessage = request.FallbackMessage.Trim();
        cfg.EnableHandoff = request.EnableHandoff;
        cfg.HandoffTriggerPhrase = SellerText.Clean(request.HandoffTriggerPhrase);
        cfg.BusinessHours = SellerText.Clean(request.BusinessHours);
        cfg.ReturnPolicy = SellerText.Clean(request.ReturnPolicy);
        cfg.WarrantyPolicy = SellerText.Clean(request.WarrantyPolicy);
        cfg.ShippingTimes = SellerText.Clean(request.ShippingTimes);
        cfg.ActivePromotions = SellerText.Clean(request.ActivePromotions);
        cfg.DiscountLevel1 = request.DiscountLevel1;
        cfg.DiscountLevel2 = request.DiscountLevel2;
        cfg.DiscountLevel3 = request.DiscountLevel3;
        cfg.MaxDiscountPercent = request.MaxDiscountPercent;
        cfg.LoyaltyDiscount = request.LoyaltyDiscount;
        cfg.BundleDiscount = request.BundleDiscount;
        cfg.FreeShippingThreshold = request.FreeShippingThreshold;
        cfg.UpdatedAt = DateTime.UtcNow;

        if (!await TrySaveNewConfigAsync(cfg, ct))
            return ConflictProblem("Otro usuario guardó la configuración al mismo tiempo. Recarga la página e inténtalo de nuevo.");

        return await ToDtoAsync(cfg, persisted: true, ct);
    }

    // ── Respuestas fijas (BotIntent) ─────────────────────────────────────────

    /// <summary>Respuestas fijas del negocio, de mayor a menor prioridad.</summary>
    [HttpGet("quick-replies")]
    [ProducesResponseType<IReadOnlyList<SellerRulesQuickReplyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SellerRulesQuickReplyDto>>> ListQuickReplies(CancellationToken ct)
    {
        var rows = await db.BotIntents.AsNoTracking()
            .Where(i => i.BotConfig.BusinessId == BusinessId)
            .OrderByDescending(i => i.Priority).ThenBy(i => i.Id)
            .Select(i => new { i.Id, i.IntentName, i.TriggerPhrases, i.Response, i.Priority, i.IsActive, i.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(r => new SellerRulesQuickReplyDto(r.Id, r.IntentName, SellerText.ParsePhrases(r.TriggerPhrases),
            r.Response, r.Priority, r.IsActive, r.CreatedAt)).ToList();
    }

    [HttpPost("quick-replies"), RequireBusinessAdmin]
    [ProducesResponseType<SellerRulesQuickReplyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerRulesQuickReplyDto>> CreateQuickReply(SellerRulesQuickReplyUpsertRequest request, CancellationToken ct)
    {
        // Las respuestas fijas cuelgan de BotConfig: se crea con valores por defecto si el negocio aún no la tiene.
        var cfg = await db.BotConfigs.FirstOrDefaultAsync(b => b.BusinessId == BusinessId, ct);
        if (cfg == null)
        {
            cfg = new BotConfig { BusinessId = BusinessId, CreatedAt = DateTime.UtcNow };
            db.BotConfigs.Add(cfg);
        }

        var intent = new BotIntent { BotConfig = cfg, CreatedAt = DateTime.UtcNow };
        Apply(intent, request);
        db.BotIntents.Add(intent);

        if (!await TrySaveNewConfigAsync(cfg, ct))
            return ConflictProblem("Otro usuario modificó la configuración al mismo tiempo. Inténtalo de nuevo.");

        return CreatedAtAction(nameof(ListQuickReplies), null, ToDto(intent));
    }

    [HttpPut("quick-replies/{id:int}"), RequireBusinessAdmin]
    [ProducesResponseType<SellerRulesQuickReplyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SellerRulesQuickReplyDto>> UpdateQuickReply(int id, SellerRulesQuickReplyUpsertRequest request, CancellationToken ct)
    {
        var intent = await db.BotIntents.FirstOrDefaultAsync(i => i.Id == id && i.BotConfig.BusinessId == BusinessId, ct);
        if (intent == null) return NotFoundProblem("La respuesta fija no existe o fue eliminada.");

        Apply(intent, request);
        await db.SaveChangesAsync(ct);
        return ToDto(intent);
    }

    [HttpDelete("quick-replies/{id:int}"), RequireBusinessAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteQuickReply(int id, CancellationToken ct)
    {
        var intent = await db.BotIntents.FirstOrDefaultAsync(i => i.Id == id && i.BotConfig.BusinessId == BusinessId, ct);
        if (intent == null) return NotFoundProblem("La respuesta fija no existe o fue eliminada.");

        db.BotIntents.Remove(intent);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void Apply(BotIntent intent, SellerRulesQuickReplyUpsertRequest request)
    {
        intent.IntentName = request.Name.Trim();
        intent.TriggerPhrases = JsonSerializer.Serialize(SellerText.CleanList(request.TriggerPhrases, splitCommas: false));
        intent.Response = request.Response.Trim();
        intent.Priority = request.Priority;
        intent.IsActive = request.IsActive;
    }

    private static SellerRulesQuickReplyDto ToDto(BotIntent i)
        => new(i.Id, i.IntentName, SellerText.ParsePhrases(i.TriggerPhrases), i.Response, i.Priority, i.IsActive, i.CreatedAt);

    /// <summary>
    /// Guarda los cambios. Si la BotConfig era nueva y otra petición la creó a la vez, el índice único
    /// por negocio rechaza el INSERT y se devuelve false para responder 409.
    /// </summary>
    private async Task<bool> TrySaveNewConfigAsync(BotConfig cfg, CancellationToken ct)
    {
        var isNew = db.Entry(cfg).State == EntityState.Added;
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException) when (isNew)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private async Task<SellerRulesDto> ToDtoAsync(BotConfig c, bool persisted, CancellationToken ct)
    {
        // El bot usa el país del vendedor (o el del negocio) para su zona horaria: se replica aquí.
        var vendorCountry = await db.VendorConfigs.AsNoTracking()
            .Where(v => v.BusinessId == BusinessId)
            .Select(v => v.Country)
            .FirstOrDefaultAsync(ct);
        var country = SellerText.Clean(vendorCountry) ?? SellerText.Clean(Tenant.Country) ?? BusinessLocale.DefaultCountry;
        var status = Services.Bot.BusinessHours.IsOpen(c.BusinessHours, BusinessLocale.ToLocal(DateTime.UtcNow, country)) switch
        {
            true => SellerRulesHoursStatus.Open,
            false => SellerRulesHoursStatus.Closed,
            null => SellerRulesHoursStatus.Unknown,
        };

        return new SellerRulesDto(
            c.IsActive, c.BotName, c.FallbackMessage, c.EnableHandoff, c.HandoffTriggerPhrase,
            c.BusinessHours, c.ReturnPolicy, c.WarrantyPolicy, c.ShippingTimes, c.ActivePromotions,
            c.DiscountLevel1, c.DiscountLevel2, c.DiscountLevel3, c.MaxDiscountPercent,
            c.LoyaltyDiscount, c.BundleDiscount, c.FreeShippingThreshold,
            Tenant.CurrencySymbol, status, persisted ? c.UpdatedAt ?? c.CreatedAt : null);
    }
}
