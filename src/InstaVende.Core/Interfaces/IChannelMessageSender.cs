using InstaVende.Core.Enums;
namespace InstaVende.Core.Interfaces;
public interface IChannelMessageSender
{
    ChannelType Channel { get; }
    Task SendTextAsync(int businessId, string recipientExternalId, string text);
}
