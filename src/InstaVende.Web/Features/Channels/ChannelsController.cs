using System.Net.Http.Headers;
using System.Text.Json;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Services;
using InstaVende.Web.Controllers;
using InstaVende.Web.Services;
using InstaVende.Web.Services.Channels;
using InstaVende.Web.Services.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Features.Channels;

/// <summary>
/// Canales de venta del negocio: resumen, configuración de Meta (WhatsApp Cloud API, Messenger, Instagram),
/// prueba de conexión y desconexión. La vinculación de WhatsApp por QR está en <see cref="WhatsAppSessionController"/>.
/// Los secretos nunca salen del servidor: solo se informa si existen y sus últimos 4 caracteres.
/// </summary>
[Route("api/channels")]
public sealed class ChannelsController(
    AppDbContext db,
    ChannelConfigProvider channelProvider,
    DataProtectionService dataProtection,
    WhatsAppClientApi waClient,
    WaClientSupervisor supervisor,
    IHttpClientFactory httpClientFactory,
    IOptions<AppOptions> appOptions,
    ILogger<ChannelsController> logger) : TenantController
{
    private static readonly TimeSpan NodeProbeTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan GraphTimeout = TimeSpan.FromSeconds(10);

    private ChannelStore Store => field ??= new ChannelStore(db, channelProvider, logger);

    // ── Resumen ──────────────────────────────────────────────────────────────

    /// <summary>Estado de los tres canales y del servicio de sesiones QR.</summary>
    [HttpGet]
    [ProducesResponseType<ChannelsOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChannelsOverviewDto>> GetOverview()
    {
        var ct = HttpContext.RequestAborted;
        var healthTask = waClient.IsHealthyAsync(ct);
        var configs = await Store.ListAsync(BusinessId, ct);
        var whatsApp = configs.FirstOrDefault(c => c.ChannelType == ChannelType.WhatsApp);

        // Solo se consulta la sesión si WhatsApp QR está activo (consultar crea la sesión en wa-client).
        WaSessionStatus? session = null;
        if (ChannelConfigRules.IsWhatsAppQrActive(whatsApp))
            session = await ProbeSessionAsync(ct);

        var healthy = await healthTask;
        var baseUrl = PublicBaseUrl();
        var channels = Enum.GetValues<ChannelType>()
            .Select(type => ChannelConfigRules.BuildSummary(type, configs.FirstOrDefault(c => c.ChannelType == type),
                type == ChannelType.WhatsApp ? session : null, dataProtection, baseUrl, BusinessId, Tenant.IsAdmin))
            .ToList();

        return new ChannelsOverviewDto(channels, ChannelConfigRules.ServiceStatus(supervisor, healthy));
    }

    // ── Configuración de Meta ────────────────────────────────────────────────

    /// <summary>
    /// Guarda las credenciales de Meta del canal. Un token o clave secreta vacíos conservan el valor actual.
    /// Para activar el canal deben estar configurados el token de acceso y la clave secreta de la app.
    /// </summary>
    [HttpPut("{channelType}/meta-config"), RequireBusinessAdmin]
    [ProducesResponseType<ChannelSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChannelSummaryDto>> SaveMetaConfig(ChannelType channelType, ChannelMetaConfigRequest request)
    {
        if (!Enum.IsDefined(channelType)) return NotFoundProblem("El canal no existe.");
        var ct = HttpContext.RequestAborted;

        var accountId = ChannelConfigRules.Clean(channelType switch
        {
            ChannelType.WhatsApp          => request.PhoneNumberId,
            ChannelType.FacebookMessenger => request.PageId,
            _                             => request.InstagramAccountId,
        });
        if (accountId == null)
        {
            var (field, message) = channelType switch
            {
                ChannelType.WhatsApp          => (nameof(request.PhoneNumberId), "Ingresa el identificador del número de teléfono."),
                ChannelType.FacebookMessenger => (nameof(request.PageId), "Ingresa el identificador de la página de Facebook."),
                _                             => (nameof(request.InstagramAccountId), "Ingresa el identificador de la cuenta de Instagram."),
            };
            ModelState.AddModelError(field, message);
            return ValidationProblem(ModelState);
        }

        var cfg = await Store.FindAsync(BusinessId, channelType, ct);
        var switchingFromQr = channelType == ChannelType.WhatsApp && ChannelConfigRules.IsWhatsAppQrActive(cfg);
        if (switchingFromQr && !request.IsActive)
            return ConflictProblem("Tu WhatsApp está vinculado por código QR. Para usar la API oficial, guarda la configuración " +
                                   "con el canal activado (se cerrará la sesión QR) o desconecta primero el QR.");

        cfg ??= Store.Create(BusinessId, channelType);
        var wasActive = cfg.IsActive && !switchingFromQr;

        switch (channelType)
        {
            case ChannelType.WhatsApp:
                if (cfg.PhoneNumberId != accountId) cfg.PhoneNumber = null; // el número visible se obtiene al probar
                cfg.PhoneNumberId = accountId;
                cfg.WabaId = ChannelConfigRules.Clean(request.WabaId);
                break;
            case ChannelType.FacebookMessenger:
                cfg.PageId = accountId;
                break;
            default:
                cfg.InstagramAccountId = accountId;
                break;
        }

        var accessToken = ChannelConfigRules.Clean(request.AccessToken);
        var appSecret = ChannelConfigRules.Clean(request.AppSecret);
        if (accessToken != null) cfg.AccessTokenEncrypted = dataProtection.Encrypt(accessToken);
        if (appSecret != null) cfg.AppSecretEncrypted = dataProtection.Encrypt(appSecret);
        if (string.IsNullOrEmpty(cfg.WebhookVerifyToken)) cfg.WebhookVerifyToken = ChannelConfigRules.NewVerifyToken();

        if (request.IsActive)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(cfg.AccessTokenEncrypted)) missing.Add("el token de acceso");
            if (string.IsNullOrWhiteSpace(cfg.AppSecretEncrypted)) missing.Add("la clave secreta de la app (App Secret)");
            if (missing.Count > 0)
            {
                db.ChangeTracker.Clear();
                return ConflictProblem($"Para activar el canal falta {string.Join(" y ", missing)}. " +
                                       "Meta firma cada mensaje con la clave secreta y sin ella los mensajes se rechazan por seguridad.");
            }
            if (!wasActive)
            {
                cfg.ConnectedAt = DateTime.UtcNow;
                await Store.MarkChannelConnectedAsync(BusinessId, ct);
            }
            cfg.IsActive = true;
        }
        else
        {
            ChannelStore.Deactivate(cfg);
        }

        if (!await Store.SaveAsync(BusinessId, ct))
            return ConflictProblem("Otro cambio se guardó al mismo tiempo. Recarga la página e inténtalo de nuevo.");

        if (switchingFromQr && !await waClient.DisconnectAsync(BusinessId, ct))
            logger.LogWarning("No se pudo cerrar la sesión QR del negocio {BusinessId} al activar la Cloud API", BusinessId);

        logger.LogInformation("Configuración de {Channel} guardada para el negocio {BusinessId} (activo: {Active})",
            channelType, BusinessId, cfg.IsActive);
        return ChannelConfigRules.BuildSummary(channelType, cfg, null, dataProtection, PublicBaseUrl(), BusinessId, includeVerifyToken: true);
    }

    /// <summary>Genera un nuevo token de verificación del webhook (crea la configuración si no existe).</summary>
    [HttpPost("{channelType}/regenerate-verify-token"), RequireBusinessAdmin]
    [ProducesResponseType<ChannelSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChannelSummaryDto>> RegenerateVerifyToken(ChannelType channelType)
    {
        if (!Enum.IsDefined(channelType)) return NotFoundProblem("El canal no existe.");
        var ct = HttpContext.RequestAborted;

        var cfg = await Store.FindAsync(BusinessId, channelType, ct) ?? Store.Create(BusinessId, channelType);
        cfg.WebhookVerifyToken = ChannelConfigRules.NewVerifyToken();
        if (!await Store.SaveAsync(BusinessId, ct))
            return ConflictProblem("Otro cambio se guardó al mismo tiempo. Recarga la página e inténtalo de nuevo.");

        var session = ChannelConfigRules.IsWhatsAppQrActive(cfg) ? await ProbeSessionAsync(ct) : null;
        return ChannelConfigRules.BuildSummary(channelType, cfg, session, dataProtection, PublicBaseUrl(), BusinessId, includeVerifyToken: true);
    }

    /// <summary>Desconecta el canal (lo desactiva conservando sus credenciales). En WhatsApp QR cierra también la sesión.</summary>
    [HttpDelete("{channelType}"), RequireBusinessAdmin]
    [ProducesResponseType<ChannelActionResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChannelActionResultDto>> Disconnect(ChannelType channelType)
    {
        if (!Enum.IsDefined(channelType)) return NotFoundProblem("El canal no existe.");
        if (channelType == ChannelType.WhatsApp)
            return await WhatsAppSessionController.DisconnectWhatsAppAsync(Store, waClient, BusinessId, HttpContext.RequestAborted);

        var cfg = await Store.FindAsync(BusinessId, channelType, HttpContext.RequestAborted);
        if (cfg is { IsActive: true })
        {
            ChannelStore.Deactivate(cfg);
            await Store.SaveAsync(BusinessId, HttpContext.RequestAborted);
        }
        return new ChannelActionResultDto(true, "Canal desconectado. Tu vendedor dejará de responder por este canal.");
    }

    // ── Prueba de conexión ───────────────────────────────────────────────────

    /// <summary>
    /// Comprueba la conexión guardada. Meta: valida el token consultando la cuenta en Graph API.
    /// WhatsApp QR: comprueba que la sesión esté conectada.
    /// </summary>
    [HttpPost("{channelType}/test"), RequireBusinessAdmin]
    [ProducesResponseType<ChannelTestResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChannelTestResultDto>> Test(ChannelType channelType)
    {
        if (!Enum.IsDefined(channelType)) return NotFoundProblem("El canal no existe.");
        var ct = HttpContext.RequestAborted;

        var cfg = await Store.FindAsync(BusinessId, channelType, ct);
        if (cfg == null)
            return new ChannelTestResultDto(false, "Primero guarda la configuración del canal.", null);

        if (channelType == ChannelType.WhatsApp && !ChannelConfigRules.IsWhatsAppCloud(cfg))
            return await TestWhatsAppQrAsync(cfg, ct);

        var accountId = ChannelConfigRules.MetaAccountId(cfg);
        if (string.IsNullOrWhiteSpace(accountId))
            return new ChannelTestResultDto(false, "Falta el identificador de la cuenta de Meta.", null);
        var token = string.IsNullOrWhiteSpace(cfg.AccessTokenEncrypted) ? null : dataProtection.TryDecrypt(cfg.AccessTokenEncrypted);
        if (token == null)
            return new ChannelTestResultDto(false, "No hay un token de acceso válido guardado. Ingrésalo de nuevo y guarda los cambios.", null);

        var result = await QueryGraphAccountAsync(channelType, accountId, token, ct);
        if (!result.Ok) return new ChannelTestResultDto(false, result.Message, null);

        if (channelType == ChannelType.WhatsApp && result.Phone != null && cfg.PhoneNumber != result.Phone)
        {
            cfg.PhoneNumber = result.Phone;
            await Store.SaveAsync(BusinessId, ct);
        }

        var message = $"Conexión correcta con «{result.AccountName ?? accountId}».";
        if (string.IsNullOrWhiteSpace(cfg.AppSecretEncrypted))
            message += " Aún falta la clave secreta de la app para poder recibir mensajes.";
        else if (!cfg.IsActive)
            message += " Activa el canal para que tu vendedor empiece a responder.";
        return new ChannelTestResultDto(true, message, result.AccountName);
    }

    private async Task<ChannelTestResultDto> TestWhatsAppQrAsync(ChannelConfig cfg, CancellationToken ct)
    {
        if (!cfg.IsActive)
            return new ChannelTestResultDto(false, "WhatsApp no está vinculado. Escanea el código QR para conectarlo.", null);

        var status = await ProbeSessionAsync(ct);
        if (status.Connected)
        {
            var phone = ChannelConfigRules.NormalizePhone(status.Info?.Phone) ?? cfg.PhoneNumber;
            return new ChannelTestResultDto(true,
                phone != null ? $"WhatsApp está conectado con el número +{phone}." : "WhatsApp está conectado.",
                status.Info?.Pushname);
        }
        return status.State == ChannelConfigRules.OfflineState
            ? new ChannelTestResultDto(false, "El servicio de WhatsApp no responde. Reinícialo desde la página de WhatsApp.", null)
            : new ChannelTestResultDto(false, "La sesión de WhatsApp no está conectada. Vuelve a escanear el código QR.", null);
    }

    private sealed record GraphProbe(bool Ok, string Message, string? AccountName, string? Phone);

    private async Task<GraphProbe> QueryGraphAccountAsync(ChannelType channel, string accountId, string token, CancellationToken ct)
    {
        var fields = channel switch
        {
            ChannelType.WhatsApp          => "id,display_phone_number,verified_name",
            ChannelType.FacebookMessenger => "id,name",
            _                             => "id,username,name",
        };
        // accountId ya está validado como numérico; el token va en la cabecera, nunca en la URL.
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://graph.facebook.com/{MetaGraphClient.ApiVersion}/{Uri.EscapeDataString(accountId)}?fields={fields}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(GraphTimeout);
        try
        {
            var http = httpClientFactory.CreateClient(nameof(ChannelsController));
            using var response = await http.SendAsync(request, cts.Token);
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cts.Token), cancellationToken: cts.Token);
            var root = body.RootElement;

            if (!response.IsSuccessStatusCode || root.TryGetProperty("error", out _))
            {
                var (code, metaMessage) = ReadGraphError(root);
                logger.LogInformation("Prueba de {Channel} fallida para el negocio {BusinessId}: Graph {Status} código {Code}",
                    channel, BusinessId, (int)response.StatusCode, code);
                return new GraphProbe(false, GraphErrorMessage(code, metaMessage, accountId), null, null);
            }

            string? Str(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var name = channel switch
            {
                ChannelType.WhatsApp => Str("verified_name") ?? Str("display_phone_number"),
                ChannelType.Instagram => Str("username") is { } u ? "@" + u : Str("name"),
                _ => Str("name"),
            };
            var phone = channel == ChannelType.WhatsApp ? ChannelConfigRules.NormalizePhone(Str("display_phone_number")) : null;
            return new GraphProbe(true, "Conexión correcta.", name, phone);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "No se pudo contactar con Graph API al probar {Channel} (negocio {BusinessId})", channel, BusinessId);
            return new GraphProbe(false, "No se pudo contactar con Meta. Revisa tu conexión e inténtalo en unos minutos.", null, null);
        }
    }

    private static (int? Code, string? Message) ReadGraphError(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object) return (null, null);
        int? code = error.TryGetProperty("code", out var c) && c.TryGetInt32(out var n) ? n : null;
        var message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        return (code, message);
    }

    private static string GraphErrorMessage(int? code, string? metaMessage, string accountId) => code switch
    {
        190 => "El token de acceso no es válido o expiró. Genera uno nuevo en Meta (recomendado: token permanente de un usuario del sistema).",
        100 => $"Meta no encontró la cuenta {accountId} o el token no tiene acceso a ella. Revisa el identificador y los permisos del token.",
        10 or 200 or 299 => "El token no tiene los permisos necesarios para esta cuenta. Revisa los permisos al generarlo.",
        4 or 17 or 32 or 613 => "Meta está limitando las solicitudes. Espera unos minutos y vuelve a probar.",
        _ => metaMessage is { Length: > 0 } ? $"Meta rechazó la conexión: {metaMessage}" : "Meta rechazó la conexión.",
    };

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Estado de la sesión QR con un tiempo máximo corto (no bloquear la página si wa-client está colgado).</summary>
    private async Task<WaSessionStatus> ProbeSessionAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(NodeProbeTimeout);
        try
        {
            return await waClient.GetStatusAsync(BusinessId, null, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return WaSessionStatus.Offline;
        }
    }

    private string PublicBaseUrl()
    {
        var configured = appOptions.Value.PublicBaseUrl;
        return string.IsNullOrWhiteSpace(configured) ? $"{Request.Scheme}://{Request.Host}" : configured.TrimEnd('/');
    }
}
