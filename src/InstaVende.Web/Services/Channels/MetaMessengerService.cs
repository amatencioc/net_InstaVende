using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Web.Services.Messaging;

namespace InstaVende.Web.Services.Channels;

/// <summary>Base común de Messenger e Instagram: ambos usan la Send API de Meta con el mismo payload.</summary>
public abstract class MetaGraphSenderBase : IChannelMessageSender
{
    private readonly ChannelConfigProvider _configs;
    private readonly MetaGraphClient _graph;

    protected MetaGraphSenderBase(ChannelConfigProvider configs, MetaGraphClient graph)
    {
        _configs = configs;
        _graph = graph;
    }

    public abstract ChannelType Channel { get; }

    /// <summary>Endpoint de envío para la configuración dada; null si falta algún dato.</summary>
    protected abstract string? ResolvePath(ChannelSnapshot cfg);

    public async Task<SendResult> SendTextAsync(int businessId, string recipientExternalId, string text, CancellationToken ct = default)
    {
        var cfg = await _configs.GetAsync(businessId, Channel, ct);
        if (cfg is not { IsActive: true } || string.IsNullOrEmpty(cfg.AccessToken))
            return SendResult.Fail($"{Channel} no está activo o le falta el token de acceso.");

        var path = ResolvePath(cfg);
        if (path == null) return SendResult.Fail($"Falta configurar el identificador de la cuenta de {Channel}.");

        return await _graph.PostMessageAsync(path, cfg.AccessToken, new
        {
            recipient      = new { id = recipientExternalId },
            messaging_type = "RESPONSE",
            message        = new { text },
        }, ct);
    }
}

public sealed class MetaMessengerService(ChannelConfigProvider configs, MetaGraphClient graph) : MetaGraphSenderBase(configs, graph)
{
    public override ChannelType Channel => ChannelType.FacebookMessenger;
    protected override string ResolvePath(ChannelSnapshot cfg) => "me/messages";
}
