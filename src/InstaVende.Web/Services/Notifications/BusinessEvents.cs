using System.Threading.Channels;

namespace InstaVende.Web.Services.Notifications;

public enum BusinessEventType { HandoffRequested, OrderCreated }

/// <summary>Evento relevante para el comercio (se notifica en tiempo real y por email).</summary>
public sealed record BusinessEvent(
    int BusinessId,
    BusinessEventType Type,
    string Title,
    string Message,
    string? Link = null, // ruta del panel relacionada (p.ej. "/orders/15"), relativa al SPA
    int? ConversationId = null,
    int? OrderId = null);

/// <summary>
/// Publicación desacoplada de eventos de negocio: quien publica no espera el envío de emails.
/// Consumido por <see cref="BusinessEventDispatcher"/>.
/// </summary>
public sealed class BusinessEvents
{
    private readonly Channel<BusinessEvent> _channel =
        Channel.CreateBounded<BusinessEvent>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Publish(BusinessEvent e) => _channel.Writer.TryWrite(e);

    internal ChannelReader<BusinessEvent> Reader => _channel.Reader;
}
