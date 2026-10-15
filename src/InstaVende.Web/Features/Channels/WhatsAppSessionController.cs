using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using InstaVende.Web.Services.Channels;
using InstaVende.Web.Services.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace InstaVende.Web.Features.Channels;

/// <summary>
/// Vinculación de WhatsApp por código QR (sesión de WhatsApp Web en wa-client, una por negocio).
/// El SPA consulta el estado periódicamente; al detectar la sesión conectada el servidor activa el canal
/// con el teléfono que informa wa-client (nunca con datos del cliente).
/// </summary>
[Route("api/channels")]
[RequireBusinessAdmin]
public sealed class WhatsAppSessionController(
    AppDbContext db,
    ChannelConfigProvider channelProvider,
    WhatsAppClientApi waClient,
    WaClientSupervisor supervisor,
    ILogger<WhatsAppSessionController> logger) : TenantController
{
    private ChannelStore Store => field ??= new ChannelStore(db, channelProvider, logger);

    /// <summary>
    /// Estado de la sesión QR. Crea la sesión en wa-client si no existe.
    /// Con <paramref name="qrHash"/> igual al QR actual no se reenvía la imagen (<c>unchanged = true</c>).
    /// </summary>
    [HttpGet("whatsapp/session")]
    [ProducesResponseType<WhatsAppSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WhatsAppSessionDto>> GetSession(
        [FromQuery, RegularExpression("^[a-fA-F0-9]{1,64}$", ErrorMessage = "qrHash no es válido.")] string? qrHash)
    {
        var ct = HttpContext.RequestAborted;
        var cfg = await Store.FindAsync(BusinessId, ChannelType.WhatsApp, ct);
        if (ChannelConfigRules.IsWhatsAppCloudActive(cfg))
            return ConflictProblem("Tu WhatsApp ya está conectado con la API oficial de Meta (Cloud API). " +
                                   "Desactívala antes de vincular un número con código QR.");

        var status = await waClient.GetStatusAsync(BusinessId, qrHash, ct);
        if (status.Connected)
            cfg = await Store.SyncQrConnectedAsync(BusinessId, status, ct);

        var offline = status.State == ChannelConfigRules.OfflineState;
        return new WhatsAppSessionDto(
            status.State,
            status.Connected,
            status.QrDataUrl,
            status.QrExpiresAt,
            status.QrExpiresAt is { } expires ? (int)Math.Clamp((expires - DateTimeOffset.UtcNow).TotalSeconds, 0, 300) : null,
            status.QrHash,
            status.Unchanged,
            ChannelConfigRules.NormalizePhone(status.Info?.Phone) ?? (status.Connected ? cfg?.PhoneNumber : null),
            status.Info?.Pushname,
            status.Connected ? cfg?.ConnectedAt : null,
            ChannelConfigRules.ServiceStatus(supervisor, healthy: !offline));
    }

    /// <summary>Cierra la sesión de WhatsApp en wa-client y desactiva el canal.</summary>
    [HttpPost("whatsapp/disconnect")]
    [ProducesResponseType<ChannelActionResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChannelActionResultDto>> Disconnect()
        => await DisconnectWhatsAppAsync(Store, waClient, BusinessId, HttpContext.RequestAborted);

    /// <summary>Descarta la sesión actual y genera un código QR nuevo.</summary>
    [HttpPost("whatsapp/reset")]
    [ProducesResponseType<ChannelActionResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ChannelActionResultDto>> Reset()
    {
        var ct = HttpContext.RequestAborted;
        var cfg = await Store.FindAsync(BusinessId, ChannelType.WhatsApp, ct);
        if (ChannelConfigRules.IsWhatsAppCloudActive(cfg))
            return ConflictProblem("Tu WhatsApp está conectado con la API oficial de Meta (Cloud API); no usa código QR.");

        if (!await waClient.ResetAsync(BusinessId, ct))
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "El servicio de WhatsApp no respondió. Espera unos segundos o reinícialo e inténtalo de nuevo.");

        // La sesión anterior ya no existe: el canal queda desconectado hasta escanear el QR nuevo.
        if (cfg is { IsActive: true })
        {
            ChannelStore.Deactivate(cfg);
            await Store.SaveAsync(BusinessId, ct);
        }
        return new ChannelActionResultDto(true, "Generando un código QR nuevo…");
    }

    /// <summary>
    /// Reinicia el proceso wa-client cuando no responde. El proceso es compartido por todos los negocios,
    /// por eso solo se permite si está caído y la app lo gestiona.
    /// </summary>
    [HttpPost("service/restart")]
    [ProducesResponseType<ChannelActionResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChannelActionResultDto>> RestartService()
    {
        var ct = HttpContext.RequestAborted;
        if (!supervisor.IsManaged)
            return ConflictProblem("El servicio de WhatsApp se gestiona fuera de la aplicación. Pide al administrador del servidor que lo reinicie.");
        if (supervisor.IsRestarting)
            return new ChannelActionResultDto(true, "El servicio ya se está reiniciando. El código QR aparecerá en unos segundos.");
        if (await waClient.IsHealthyAsync(ct))
            return ConflictProblem("El servicio de WhatsApp está funcionando, no hace falta reiniciarlo. " +
                                   "Si el código QR no aparece, usa «Generar QR nuevo».");

        logger.LogWarning("Reinicio manual de wa-client solicitado por el negocio {BusinessId}", BusinessId);
        await supervisor.RestartAsync(ct);
        return new ChannelActionResultDto(true, "Servicio reiniciado. El código QR aparecerá en unos segundos.");
    }

    /// <summary>Desconexión de WhatsApp compartida con <c>DELETE /api/channels/WhatsApp</c>.</summary>
    internal static async Task<ChannelActionResultDto> DisconnectWhatsAppAsync(ChannelStore store, WhatsAppClientApi waClient,
        int businessId, CancellationToken ct)
    {
        var cfg = await store.FindAsync(businessId, ChannelType.WhatsApp, ct);
        var usesQr = cfg == null || !ChannelConfigRules.IsWhatsAppCloud(cfg);

        if (cfg is { IsActive: true })
        {
            ChannelStore.Deactivate(cfg);
            await store.SaveAsync(businessId, ct);
        }

        if (usesQr && !await waClient.DisconnectAsync(businessId, ct))
            return new ChannelActionResultDto(false,
                "Desactivamos WhatsApp, pero el servicio de WhatsApp no respondió para cerrar la sesión. " +
                "Por seguridad, ciérrala desde tu celular en Ajustes › Dispositivos vinculados.");

        return new ChannelActionResultDto(true, "WhatsApp desconectado. Tu vendedor dejará de responder por este canal.");
    }
}
