using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
namespace InstaVende.Core.Interfaces;
public interface IConversationRepository : IRepository<Conversation>
{
    Task<IEnumerable<Conversation>> GetByBusinessIdAsync(int businessId, ChannelType? channel = null, ConversationStatus? status = null);
    Task<Conversation?> GetWithMessagesAsync(int conversationId);
    Task<Conversation?> FindByContactAndChannelAsync(int contactId, ChannelType channel);
}
