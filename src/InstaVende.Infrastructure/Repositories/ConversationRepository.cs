using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Infrastructure.Repositories;

public class ConversationRepository : Repository<Conversation>, IConversationRepository
{
    public ConversationRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Conversation>> GetByBusinessIdAsync(int businessId, ChannelType? channel = null, ConversationStatus? status = null)
    {
        var query = _context.Conversations
            .Include(c => c.Contact)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(c => c.BusinessId == businessId);
        if (channel.HasValue) query = query.Where(c => c.ChannelType == channel.Value);
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        return await query.OrderByDescending(c => c.UpdatedAt).ToListAsync();
    }

    public async Task<Conversation?> GetWithMessagesAsync(int conversationId)
        => await _context.Conversations
            .Include(c => c.Contact)
            .Include(c => c.Messages.OrderBy(m => m.SentAt))
            .FirstOrDefaultAsync(c => c.Id == conversationId);

    public async Task<Conversation?> FindByContactAndChannelAsync(int contactId, ChannelType channel)
        => await _context.Conversations
            .Where(c => c.ContactId == contactId && c.ChannelType == channel && c.Status != ConversationStatus.Resolved)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync();
}
