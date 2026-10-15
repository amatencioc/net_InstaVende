using System.Data;
using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.SellerData;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Imagen de apoyo para pagar (código QR, captura de cuenta…). <c>Url</c> es relativa al servidor.</summary>
public sealed record PaymentImageDto(int Id, string Url, int SortOrder, DateTime CreatedAt);

/// <summary>Imágenes de pago del negocio (máximo <see cref="MaxImages"/>).</summary>
[Route("api/payment-images")]
public sealed class PaymentImagesController(AppDbContext db, ImageService images, ILogger<PaymentImagesController> logger)
    : TenantController
{
    public const int MaxImages = 5;
    private const string LimitMessage = "Ya tienes 5 imágenes de pago. Elimina alguna para subir otra.";

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PaymentImageDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentImageDto>>> List(CancellationToken ct) =>
        await db.PaymentImages.AsNoTracking()
            .Where(i => i.BusinessId == BusinessId)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new PaymentImageDto(i.Id, i.ImageUrl, i.SortOrder, i.CreatedAt))
            .ToListAsync(ct);

    /// <summary>Sube una imagen (PNG, JPG, WEBP o GIF; máx. 5 MB). El límite por negocio se valida dentro de una transacción.</summary>
    [HttpPost]
    [RequestSizeLimit(ImageService.MaxBytes + 64 * 1024)]
    [ProducesResponseType<PaymentImageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentImageDto>> Upload(IFormFile? file, CancellationToken ct)
    {
        // Comprobación rápida para no escribir en disco si ya se alcanzó el límite
        if (await db.PaymentImages.CountAsync(i => i.BusinessId == BusinessId, ct) >= MaxImages)
            return ConflictProblem(LimitMessage);

        var saved = await images.SaveAsync(file, BusinessId, "payments", ct);
        if (!saved.Success) return BadRequestProblem(saved.Error!);

        PaymentImage? image = null;
        try
        {
            // La API usa reintentos de conexión: la transacción explícita debe ir dentro de la estrategia de ejecución
            var strategy = db.Database.CreateExecutionStrategy();
            image = await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                // Serializable bloquea el rango de imágenes del negocio: dos subidas simultáneas no pueden superar el límite
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var count = await db.PaymentImages.CountAsync(i => i.BusinessId == BusinessId, ct);
                if (count >= MaxImages) return null;

                var maxOrder = await db.PaymentImages.Where(i => i.BusinessId == BusinessId)
                    .MaxAsync(i => (int?)i.SortOrder, ct) ?? -1;
                var entity = new PaymentImage
                {
                    BusinessId = BusinessId, ImageUrl = saved.Url!, SortOrder = maxOrder + 1, CreatedAt = DateTime.UtcNow,
                };
                db.PaymentImages.Add(entity);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return entity;
            });
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            // Interbloqueo entre subidas simultáneas u otro fallo: no dejar archivos huérfanos
            logger.LogWarning(ex, "No se pudo registrar la imagen de pago del negocio {BusinessId}", BusinessId);
            images.Delete(saved.Url);
            return ConflictProblem("No se pudo guardar la imagen. Inténtalo de nuevo.");
        }

        if (image == null)
        {
            images.Delete(saved.Url);
            return ConflictProblem(LimitMessage);
        }

        return StatusCode(StatusCodes.Status201Created, new PaymentImageDto(image.Id, image.ImageUrl, image.SortOrder, image.CreatedAt));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var image = await db.PaymentImages.FirstOrDefaultAsync(i => i.Id == id && i.BusinessId == BusinessId, ct);
        if (image == null) return NotFoundProblem("La imagen no existe o fue eliminada.");
        db.PaymentImages.Remove(image);
        await db.SaveChangesAsync(ct);
        images.Delete(image.ImageUrl); // el archivo se borra solo cuando el registro ya no existe
        return NoContent();
    }
}
