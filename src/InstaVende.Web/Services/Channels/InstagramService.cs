using InstaVende.Core.Enums;
using InstaVende.Web.Services.Messaging;

namespace InstaVende.Web.Services.Channels;

/// <summary>Respuestas a DMs de Instagram vía la Send API de la cuenta profesional.</summary>
public sealed class InstagramService(ChannelConfigProvider configs, MetaGraphClient graph) : MetaGraphSenderBase(configs, graph)
{
    public override ChannelType Channel => ChannelType.Instagram;

    protected override string? ResolvePath(ChannelSnapshot cfg)
        => string.IsNullOrEmpty(cfg.InstagramAccountId) ? null : $"{cfg.InstagramAccountId}/messages";
}
