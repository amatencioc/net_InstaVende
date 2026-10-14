namespace InstaVende.Web.Services.Messaging;

/// <summary>Consume <see cref="InboundMessageQueue"/> con un worker por partición.</summary>
public sealed class InboundMessageProcessor : BackgroundService
{
    private readonly InboundMessageQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<InboundMessageProcessor> _logger;

    public InboundMessageProcessor(InboundMessageQueue queue, IServiceScopeFactory scopes, ILogger<InboundMessageProcessor> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.WhenAll(Enumerable.Range(0, InboundMessageQueue.Partitions).Select(p => RunPartitionAsync(p, stoppingToken)));

    private async Task RunPartitionAsync(int partition, CancellationToken ct)
    {
        await foreach (var message in _queue.Reader(partition).ReadAllAsync(ct))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IncomingMessageHandler>();
                await handler.HandleAsync(message, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando mensaje entrante (negocio {BusinessId}, canal {Channel})",
                    message.BusinessId, message.Channel);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Complete();
        return base.StopAsync(cancellationToken);
    }
}
