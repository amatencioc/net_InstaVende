using InstaVende.Core.Enums;

namespace InstaVende.Web.Services.Messaging;

/// <summary>Mensaje entrante normalizado, independiente del canal de origen.</summary>
public sealed record InboundMessage(
    int BusinessId,
    ChannelType Channel,
    string ExternalUserId,
    string? ExternalMessageId,
    string Text,
    string? DisplayName);
