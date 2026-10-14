using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstaVende.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services.Channels;

/// <summary>Estado de la sesión QR de un negocio tal como lo informa wa-client.</summary>
public sealed record WaSessionStatus(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("qrDataUrl")] string? QrDataUrl,
    [property: JsonPropertyName("qrExpiresAt")] DateTimeOffset? QrExpiresAt,
    [property: JsonPropertyName("qrHash")] string? QrHash,
    [property: JsonPropertyName("unchanged")] bool Unchanged,
    [property: JsonPropertyName("info")] WaSessionInfo? Info)
{
    public static readonly WaSessionStatus Offline = new("offline", false, null, null, null, false, null);
}

public sealed record WaSessionInfo(
    [property: JsonPropertyName("wid")] string? Wid,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("pushname")] string? Pushname);

/// <summary>
/// Cliente tipado del proceso Node wa-client (contrato en wa-client/README.md).
/// Todas las llamadas llevan la cabecera X-WA-Key con el secreto compartido.
/// </summary>
public sealed class WhatsAppClientApi
{
    private readonly HttpClient _http;
    private readonly ILogger<WhatsAppClientApi> _logger;

    public WhatsAppClientApi(HttpClient http, IOptions<WhatsAppClientOptions> options, ILogger<WhatsAppClientApi> logger)
    {
        var o = options.Value;
        _http = http;
        _http.BaseAddress = new Uri(o.BaseUrl + "/");
        _http.Timeout = TimeSpan.FromSeconds(o.RequestTimeoutSeconds);
        _http.DefaultRequestHeaders.Add("X-WA-Key", o.SharedSecret);
        _logger = logger;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var resp = await _http.GetAsync("health", cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task<WaSessionStatus> GetStatusAsync(int businessId, string? qrHash, CancellationToken ct = default)
    {
        try
        {
            var url = $"sessions/{businessId}/status" + (string.IsNullOrEmpty(qrHash) ? "" : $"?qrHash={Uri.EscapeDataString(qrHash)}");
            return await _http.GetFromJsonAsync<WaSessionStatus>(url, ct) ?? WaSessionStatus.Offline;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "wa-client no disponible al consultar el estado del negocio {BusinessId}", businessId);
            return WaSessionStatus.Offline;
        }
    }

    public async Task<SendResult> SendAsync(int businessId, string to, string message, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync($"sessions/{businessId}/send", new { to, message }, ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
                return SendResult.Ok(body.TryGetProperty("messageId", out var id) ? id.GetString() : null);
            }
            return resp.StatusCode switch
            {
                HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound => SendResult.Fail("WhatsApp no está conectado para este negocio."),
                HttpStatusCode.Unauthorized => SendResult.Fail("wa-client rechazó el secreto compartido."),
                _ => SendResult.Fail($"wa-client respondió {(int)resp.StatusCode}."),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // No se reintenta: el POST no es idempotente y Node pudo haber enviado el mensaje.
            _logger.LogWarning(ex, "Fallo al enviar por wa-client (negocio {BusinessId})", businessId);
            return SendResult.Fail("El servicio de WhatsApp no respondió.");
        }
    }

    public Task<bool> DisconnectAsync(int businessId, CancellationToken ct = default) => PostAsync($"sessions/{businessId}/disconnect", ct);

    public Task<bool> ResetAsync(int businessId, CancellationToken ct = default) => PostAsync($"sessions/{businessId}/reset", ct);

    private async Task<bool> PostAsync(string path, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.PostAsync(path, JsonContent.Create(new { }), ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "wa-client no respondió a {Path}", path);
            return false;
        }
    }
}
