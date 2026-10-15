using InstaVende.Core.Domain;
using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Settings;

/// <summary>Datos del negocio (organización): identidad, país/moneda, contacto y logo. Solo administradores.</summary>
[Route("api/settings/business")]
[RequireBusinessAdmin]
public sealed class BusinessSettingsController(AppDbContext db, ImageService images) : TenantController
{
    /// <summary>Rubros sugeridos (el valor guardado puede ser otro texto heredado).</summary>
    private static readonly IReadOnlyList<string> Sectors =
    [
        "Ropa y calzado", "Tecnología", "Alimentos y bebidas", "Belleza y cuidado personal", "Hogar y deco",
        "Deportes", "Salud", "Servicios", "Educación", "Mascotas", "Juguetes y bebés", "Otro",
    ];

    [HttpGet]
    [ProducesResponseType<SettingsBusinessDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SettingsBusinessDto>> Get(CancellationToken ct)
    {
        var business = await db.Businesses.AsNoTracking().FirstOrDefaultAsync(b => b.Id == BusinessId, ct);
        return business == null ? NotFoundProblem() : ToDto(business);
    }

    [HttpPut]
    [ProducesResponseType<SettingsBusinessDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SettingsBusinessDto>> Update(SettingsBusinessUpdateRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) ModelState.AddModelError(nameof(request.Name), "El nombre del negocio es obligatorio.");
        var country = BusinessLocale.Countries.FirstOrDefault(c => c == request.Country.Trim());
        if (country == null) ModelState.AddModelError(nameof(request.Country), "Selecciona un país de la lista.");
        var currency = BusinessLocale.Currencies.FirstOrDefault(c => c == request.Currency.Trim());
        if (currency == null) ModelState.AddModelError(nameof(request.Currency), "Selecciona una moneda de la lista.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == BusinessId, ct);
        if (business == null) return NotFoundProblem();

        business.Name = name;
        business.Description = Clean(request.Description);
        business.Sector = Clean(request.Sector);
        business.Country = country;
        business.Currency = currency;
        business.Phone = Clean(request.Phone);
        business.Email = Clean(request.Email)?.ToLowerInvariant();
        business.WebsiteUrl = Clean(request.WebsiteUrl);
        business.SocialMedia = Clean(request.SocialMedia);
        business.WhatsAppNumber = Clean(request.WhatsAppNumber);
        await db.SaveChangesAsync(ct);
        return ToDto(business);
    }

    [HttpPost("logo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageService.MaxBytes + 64 * 1024)]
    [ProducesResponseType<SettingsImageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SettingsImageDto>> UploadLogo(IFormFile file, CancellationToken ct)
    {
        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == BusinessId, ct);
        if (business == null) return NotFoundProblem();

        var upload = await images.SaveAsync(file, BusinessId, "logos", ct);
        if (!upload.Success) return BadRequestProblem(upload.Error!);

        var previous = business.LogoUrl;
        business.LogoUrl = upload.Url;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            images.Delete(upload.Url);
            throw;
        }
        images.Delete(previous);
        return new SettingsImageDto(business.LogoUrl);
    }

    [HttpDelete("logo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteLogo(CancellationToken ct)
    {
        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == BusinessId, ct);
        if (business == null) return NotFoundProblem();
        if (business.LogoUrl == null) return NoContent();

        var previous = business.LogoUrl;
        business.LogoUrl = null;
        await db.SaveChangesAsync(ct);
        images.Delete(previous);
        return NoContent();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SettingsBusinessDto ToDto(Business b) => new(
        b.Name, b.Description, b.Sector, b.Country, b.Currency, b.Phone, b.Email, b.WebsiteUrl, b.SocialMedia,
        b.WhatsAppNumber, b.LogoUrl, BusinessLocale.Countries, BusinessLocale.Currencies, Sectors);
}
