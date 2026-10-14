using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Web.Services.Messaging;

namespace InstaVende.Web.Services.Channels;

/// <summary>
/// Envía mensajes de WhatsApp por la Cloud API oficial cuando el negocio la configuró
/// (PhoneNumberId + token) o, si no, por la sesión QR del negocio en wa-client.
/// </summary>
public sealed class WhatsAppService : IChannelMessageSender
{
    private readonly ChannelConfigProvider _configs;
    private readonly WhatsAppClientApi _waClient;
    private readonly MetaGraphClient _graph;

    public WhatsAppService(ChannelConfigProvider configs, WhatsAppClientApi waClient, MetaGraphClient graph)
    {
        _configs = configs;
        _waClient = waClient;
        _graph = graph;
    }

    public ChannelType Channel => ChannelType.WhatsApp;

    public async Task<SendResult> SendTextAsync(int businessId, string recipientExternalId, string text, CancellationToken ct = default)
    {
        var cfg = await _configs.GetAsync(businessId, ChannelType.WhatsApp, ct);
        if (cfg is not { IsActive: true }) return SendResult.Fail("WhatsApp no está activo para este negocio.");

        if (cfg.IsWhatsAppCloud)
        {
            return await _graph.PostMessageAsync($"{cfg.PhoneNumberId}/messages", cfg.AccessToken!, new
            {
                messaging_product = "whatsapp",
                recipient_type    = "individual",
                to                = recipientExternalId,
                type              = "text",
                text              = new { body = text, preview_url = true },
            }, ct);
        }

        return await _waClient.SendAsync(businessId, recipientExternalId, text, ct);
    }
}
