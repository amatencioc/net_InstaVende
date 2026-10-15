using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace InstaVende.Web.Services.Messaging;

/// <summary>Eventos en tiempo real hacia el panel (SignalR, grupo por negocio).</summary>
public sealed class InboxNotifier
{
    private readonly IHubContext<InboxHub> _hub;

    public InboxNotifier(IHubContext<InboxHub> hub) => _hub = hub;

    public Task MessageAsync(int businessId, Message message, Contact? contact = null, ChannelType? channel = null, CancellationToken ct = default)
        => _hub.Clients.Group(InboxHub.GroupName(businessId)).SendAsync("messageReceived", new
        {
            conversationId = message.ConversationId,
            message = new
            {
                id             = message.Id,
                content        = message.Content,
                mediaUrl       = message.MediaUrl,
                mediaType      = message.MediaType,
                direction      = message.Direction,
                sentByBot      = message.SentByBot,
                deliveryStatus = message.DeliveryStatus,
                sentAt         = message.SentAt,
            },
            contactName = contact?.Name ?? contact?.ExternalId,
            channel,
        }, ct);

    public Task ConversationUpdatedAsync(int businessId, int conversationId, ConversationStatus status, CancellationToken ct = default)
        => _hub.Clients.Group(InboxHub.GroupName(businessId)).SendAsync("conversationUpdated", new { conversationId, status }, ct);

    public Task MessageStatusAsync(int businessId, int conversationId, int messageId, MessageDeliveryStatus status, CancellationToken ct = default)
        => _hub.Clients.Group(InboxHub.GroupName(businessId)).SendAsync("messageStatus", new { conversationId, messageId, status }, ct);
}
