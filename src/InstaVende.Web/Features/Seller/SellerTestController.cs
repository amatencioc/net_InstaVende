using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using InstaVende.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Seller;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Mensaje de prueba para el vendedor. Cada mensaje se evalúa de forma aislada (la vista previa no
/// guarda historial ni carrito), por eso no se envía la conversación anterior.
/// </summary>
public sealed record SellerTestRequest(
    [property: Required(ErrorMessage = "Escribe un mensaje."), StringLength(1000, ErrorMessage = "El mensaje admite como máximo 1000 caracteres.")]
    string Message);

/// <param name="Reply">Respuesta del bot; null si el bot no respondería (p.ej. está pausado).</param>
/// <param name="HandoffTriggered">El mensaje habría derivado la conversación a un asesor humano.</param>
/// <param name="BotActive">Estado del bot: si está pausado no responde a los clientes.</param>
public sealed record SellerTestResponse(string? Reply, bool HandoffTriggered, bool BotActive);

// ── Controlador ──────────────────────────────────────────────────────────────

/// <summary>Vista previa del vendedor IA con la configuración guardada. No crea conversaciones, carritos ni pedidos.</summary>
[Route("api/seller/test")]
public sealed class SellerTestController(IBotEngineService bot, AppDbContext db) : TenantController
{
    [HttpPost, EnableRateLimiting(RateLimitPolicies.Llm)]
    [ProducesResponseType<SellerTestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SellerTestResponse>> Send(SellerTestRequest request, CancellationToken ct)
    {
        var message = request.Message.Trim();
        if (message.Length == 0) return BadRequestProblem("Escribe un mensaje.");

        var reply = await bot.ProcessMessageAsync(BusinessId, conversationId: -1, message, ct);

        // Sin fila BotConfig el bot está activo con valores por defecto (igual que BotContextProvider).
        var isActive = await db.BotConfigs.AsNoTracking()
            .Where(b => b.BusinessId == BusinessId)
            .Select(b => (bool?)b.IsActive)
            .FirstOrDefaultAsync(ct) ?? true;

        return new SellerTestResponse(reply.Text, reply.HandoffTriggered, isActive);
    }
}
