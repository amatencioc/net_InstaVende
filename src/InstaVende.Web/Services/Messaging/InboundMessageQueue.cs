using System.Threading.Channels;

namespace InstaVende.Web.Services.Messaging;

/// <summary>
/// Cola en memoria de mensajes entrantes. Se particiona por (negocio, contacto) para que
/// los mensajes de un mismo cliente se procesen en orden y sin carreras, mientras que
/// clientes distintos se atienden en paralelo. Los webhooks solo encolan y responden 200 al instante.
/// </summary>
public sealed class InboundMessageQueue
{
    public const int Partitions = 8;
    private const int CapacityPerPartition = 1000;

    private readonly Channel<InboundMessage>[] _channels;

    public InboundMessageQueue()
    {
        _channels = Enumerable.Range(0, Partitions)
            .Select(_ => Channel.CreateBounded<InboundMessage>(new BoundedChannelOptions(CapacityPerPartition)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait,
            }))
            .ToArray();
    }

    /// <summary>false si la partición está llena (el webhook responde 503 para que el proveedor reintente).</summary>
    public bool TryEnqueue(InboundMessage message)
    {
        var partition = (int)((uint)HashCode.Combine(message.BusinessId, message.ExternalUserId) % Partitions);
        return _channels[partition].Writer.TryWrite(message);
    }

    public ChannelReader<InboundMessage> Reader(int partition) => _channels[partition].Reader;

    public void Complete()
    {
        foreach (var c in _channels) c.Writer.TryComplete();
    }
}
