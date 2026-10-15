using InstaVende.Core.Enums;
using InstaVende.Web.Services.Messaging;

namespace InstaVende.UnitTests.Messaging;

public class InboundMessageQueueTests
{
    [Fact]
    public async Task MensajesDelMismoContacto_VanALaMismaParticionEnOrden()
    {
        var queue = new InboundMessageQueue();
        for (var i = 0; i < 5; i++)
            Assert.True(queue.TryEnqueue(new InboundMessage(1, ChannelType.WhatsApp, "519", $"id{i}", $"m{i}", null)));
        queue.Complete();

        var received = new List<string>();
        for (var p = 0; p < InboundMessageQueue.Partitions; p++)
            await foreach (var m in queue.Reader(p).ReadAllAsync(TestContext.Current.CancellationToken))
                received.Add($"{p}:{m.Text}");

        var partitions = received.Select(r => r.Split(':')[0]).Distinct().ToList();
        Assert.Single(partitions);
        Assert.Equal(["m0", "m1", "m2", "m3", "m4"], received.Select(r => r.Split(':')[1]));
    }
}
