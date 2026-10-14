using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InstaVende.Core.Interfaces;

namespace InstaVende.Web.Services.Channels;

/// <summary>Cliente tipado de la Graph API de Meta (WhatsApp Cloud, Messenger e Instagram).</summary>
public sealed class MetaGraphClient
{
    public const string ApiVersion = "v21.0";

    private readonly HttpClient _http;
    private readonly ILogger<MetaGraphClient> _logger;

    public MetaGraphClient(HttpClient http, ILogger<MetaGraphClient> logger)
    {
        _http = http;
        _http.BaseAddress = new Uri($"https://graph.facebook.com/{ApiVersion}/");
        _http.Timeout = TimeSpan.FromSeconds(15);
        _logger = logger;
    }

    /// <summary>POST a un endpoint de mensajes; devuelve el id del mensaje creado.</summary>
    public async Task<SendResult> PostMessageAsync(string path, string accessToken, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var resp = await _http.SendAsync(request, ct);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!resp.IsSuccessStatusCode)
            {
                var error = body.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m) ? m.GetString() : null;
                _logger.LogWarning("Graph API {Path} respondió {Status}: {Error}", path, (int)resp.StatusCode, error);
                return SendResult.Fail(error ?? $"Meta respondió {(int)resp.StatusCode}.");
            }
            // Messenger/Instagram: { message_id }, WhatsApp Cloud: { messages: [{ id }] }
            if (body.TryGetProperty("message_id", out var mid)) return SendResult.Ok(mid.GetString());
            if (body.TryGetProperty("messages", out var msgs) && msgs.GetArrayLength() > 0 && msgs[0].TryGetProperty("id", out var wid))
                return SendResult.Ok(wid.GetString());
            return SendResult.Ok();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Error llamando a Graph API {Path}", path);
            return SendResult.Fail("No se pudo contactar con Meta.");
        }
    }
}
