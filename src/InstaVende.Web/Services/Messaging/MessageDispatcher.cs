using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Services.Messaging;

/// <summary>
/// Envío de mensajes salientes con trazabilidad: se persiste como Pending, se envía por el
/// canal y se marca Sent/Failed con el id externo. Lo usan el bot, el inbox y los recordatorios.
/// </summary>
public class MessageDispatcher
{
    private readonly AppDbContext _db;
    private readonly IServiceProvider _services;
    private readonly InboxNotifier _notifier;
    private readonly ILogger<MessageDispatcher> _logger;

    public MessageDispatcher(AppDbContext db, IServiceProvider services, InboxNotifier notifier, ILogger<MessageDispatcher> logger)
    {
        _db = db;
        _services = services;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<Message> SendAsync(int businessId, int conversationId, string text, bool sentByBot, CancellationToken ct = default)
    {
        var target = await _db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId && c.BusinessId == businessId)
            .Select(c => new { c.ChannelType, c.Contact.ExternalId })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"La conversación {conversationId} no pertenece al negocio {businessId}.");

        var now = DateTime.UtcNow;
        var message = new Message
        {
            ConversationId = conversationId,
            Direction      = MessageDirection.Outbound,
            Content        = text,
            IsRead         = true,
            SentByBot      = sentByBot,
            SentAt         = now,
            DeliveryStatus = MessageDeliveryStatus.Pending,
        };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);
        await UpdateConversationPreviewAsync(conversationId, text, now, ct);
        await _notifier.MessageAsync(businessId, message, channel: target.ChannelType, ct: ct);

        var sender = _services.GetKeyedService<IChannelMessageSender>(target.ChannelType);
        SendResult result;
        try
        {
            result = sender == null
                ? SendResult.Fail($"No hay un emisor configurado para {target.ChannelType}.")
                : await sender.SendTextAsync(businessId, target.ExternalId, text, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error enviando mensaje por {Channel} (negocio {BusinessId})", target.ChannelType, businessId);
            result = SendResult.Fail("Error inesperado al enviar.");
        }

        message.DeliveryStatus    = result.Success ? MessageDeliveryStatus.Sent : MessageDeliveryStatus.Failed;
        message.ExternalMessageId = result.ExternalMessageId;
        await _db.SaveChangesAsync(ct);
        if (!result.Success)
            _logger.LogWarning("Mensaje {MessageId} no entregado por {Channel}: {Error}", message.Id, target.ChannelType, result.Error);

        await _notifier.MessageStatusAsync(businessId, conversationId, message.Id, message.DeliveryStatus, ct);
        return message;
    }

    private Task UpdateConversationPreviewAsync(int conversationId, string text, DateTime at, CancellationToken ct)
    {
        var preview = Preview(text);
        return _db.Conversations.Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.LastMessagePreview, preview)
                .SetProperty(c => c.LastMessageAt, at)
                .SetProperty(c => c.UpdatedAt, at), ct);
    }

    public static string Preview(string text) => text.Length > 120 ? text[..117] + "..." : text;
}
