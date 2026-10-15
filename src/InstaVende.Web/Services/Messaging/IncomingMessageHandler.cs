using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Services.Messaging;

/// <summary>
/// Procesa un mensaje entrante: idempotencia por id externo, alta/actualización de contacto
/// y conversación, notificación en tiempo real y respuesta del bot.
/// Se ejecuta en segundo plano (ver <see cref="InboundMessageProcessor"/>), nunca dentro del webhook.
/// </summary>
public class IncomingMessageHandler
{
    private readonly AppDbContext _db;
    private readonly ChannelConfigProvider _channels;
    private readonly IBotEngineService _bot;
    private readonly MessageDispatcher _dispatcher;
    private readonly InboxNotifier _notifier;
    private readonly BusinessEvents _events;
    private readonly ILogger<IncomingMessageHandler> _logger;

    public IncomingMessageHandler(AppDbContext db, ChannelConfigProvider channels, IBotEngineService bot,
        MessageDispatcher dispatcher, InboxNotifier notifier, BusinessEvents events, ILogger<IncomingMessageHandler> logger)
    {
        _events = events;
        _db = db;
        _channels = channels;
        _bot = bot;
        _dispatcher = dispatcher;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task HandleAsync(InboundMessage inbound, CancellationToken ct)
    {
        var cfg = await _channels.GetAsync(inbound.BusinessId, inbound.Channel, ct);
        if (cfg is not { IsActive: true }) return;

        if (inbound.ExternalMessageId != null &&
            await _db.Messages.AnyAsync(m => m.ExternalMessageId == inbound.ExternalMessageId, ct))
        {
            _logger.LogDebug("Mensaje duplicado ignorado ({ExternalId})", inbound.ExternalMessageId);
            return;
        }

        var contact = await UpsertContactAsync(inbound, ct);
        var conversation = await GetOrCreateConversationAsync(inbound, contact, ct);

        var now = DateTime.UtcNow;
        var message = new Message
        {
            ConversationId    = conversation.Id,
            Direction         = MessageDirection.Inbound,
            Content           = inbound.Text,
            ExternalMessageId = inbound.ExternalMessageId,
            SentAt            = now,
            DeliveryStatus    = MessageDeliveryStatus.Sent,
        };
        _db.Messages.Add(message);
        conversation.UpdatedAt          = now;
        conversation.LastMessageAt      = now;
        conversation.LastMessagePreview = MessageDispatcher.Preview(inbound.Text);
        conversation.UnreadCount++;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _logger.LogDebug("Mensaje duplicado detectado al guardar ({ExternalId})", inbound.ExternalMessageId);
            return;
        }

        await _notifier.MessageAsync(inbound.BusinessId, message, contact, inbound.Channel, ct);

        if (conversation.Status != ConversationStatus.BotActive) return;

        var reply = await _bot.ProcessMessageAsync(inbound.BusinessId, conversation.Id, inbound.Text, ct);
        if (!string.IsNullOrWhiteSpace(reply.Text))
            await _dispatcher.SendAsync(inbound.BusinessId, conversation.Id, reply.Text, sentByBot: true, ct);

        if (reply.HandoffTriggered)
        {
            var status = await _db.Conversations.Where(c => c.Id == conversation.Id).Select(c => c.Status).FirstAsync(ct);
            await _notifier.ConversationUpdatedAsync(inbound.BusinessId, conversation.Id, status, ct);
            _events.Publish(new BusinessEvent(inbound.BusinessId, BusinessEventType.HandoffRequested,
                "Un cliente necesita atención",
                $"{contact.Name ?? contact.Phone ?? "Un cliente"} pidió hablar con un asesor por {inbound.Channel}. Último mensaje: \"{MessageDispatcher.Preview(inbound.Text)}\"",
                $"/inbox/{conversation.Id}", conversation.Id));
        }
    }

    private async Task<Contact> UpsertContactAsync(InboundMessage inbound, CancellationToken ct)
    {
        var contact = await FindContactAsync(inbound, ct);
        if (contact != null)
        {
            contact.LastSeenAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(contact.Name) && !string.IsNullOrWhiteSpace(inbound.DisplayName))
                contact.Name = Truncate(inbound.DisplayName, 100);
            return contact;
        }

        contact = new Contact
        {
            BusinessId  = inbound.BusinessId,
            ChannelType = inbound.Channel,
            ExternalId  = inbound.ExternalUserId,
            Name        = Truncate(inbound.DisplayName, 100),
            Phone       = inbound.Channel == ChannelType.WhatsApp && !inbound.ExternalUserId.Contains('@') ? inbound.ExternalUserId : null,
        };
        _db.Contacts.Add(contact);
        try
        {
            await _db.SaveChangesAsync(ct);
            return contact;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Otro proceso lo creó en paralelo: se descarta el nuestro y se usa el existente
            _db.Entry(contact).State = EntityState.Detached;
            return await FindContactAsync(inbound, ct) ?? throw new InvalidOperationException("Contacto no encontrado tras conflicto.", ex);
        }
    }

    private Task<Contact?> FindContactAsync(InboundMessage inbound, CancellationToken ct)
        => _db.Contacts.FirstOrDefaultAsync(c => c.BusinessId == inbound.BusinessId
                                              && c.ChannelType == inbound.Channel
                                              && c.ExternalId == inbound.ExternalUserId, ct);

    private async Task<Conversation> GetOrCreateConversationAsync(InboundMessage inbound, Contact contact, CancellationToken ct)
    {
        var conversation = await _db.Conversations
            .Where(c => c.ContactId == contact.Id && c.ChannelType == inbound.Channel && c.Status != ConversationStatus.Resolved)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (conversation != null) return conversation;

        conversation = new Conversation { BusinessId = inbound.BusinessId, ContactId = contact.Id, ChannelType = inbound.Channel };
        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(ct);
        return conversation;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static string? Truncate(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}
