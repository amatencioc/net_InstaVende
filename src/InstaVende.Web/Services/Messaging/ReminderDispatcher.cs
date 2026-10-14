using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Services.Messaging;

/// <summary>
/// Envía cada minuto los recordatorios vencidos (Status = Pending y ScheduledAt &lt;= ahora)
/// por el canal del contacto y los marca como Sent o Failed.
/// </summary>
public sealed class ReminderDispatcher : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReminderDispatcher> _logger;

    public ReminderDispatcher(IServiceScopeFactory scopes, ILogger<ReminderDispatcher> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await DispatchDueAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error al despachar recordatorios"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchDueAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<MessageDispatcher>();

        var due = await db.Reminders
            .Include(r => r.Contact)
            .Where(r => r.Status == ReminderStatus.Pending && r.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(r => r.ScheduledAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var reminder in due)
        {
            try
            {
                var conversationId = await ResolveConversationAsync(db, reminder, ct);
                var message = await dispatcher.SendAsync(reminder.BusinessId, conversationId, reminder.Message, sentByBot: true, ct);
                reminder.Status = message.DeliveryStatus == MessageDeliveryStatus.Sent ? ReminderStatus.Sent : ReminderStatus.Failed;
                reminder.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo enviar el recordatorio {ReminderId}", reminder.Id);
                reminder.Status = ReminderStatus.Failed;
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task<int> ResolveConversationAsync(AppDbContext db, Reminder reminder, CancellationToken ct)
    {
        if (reminder.ConversationId is int id &&
            await db.Conversations.AnyAsync(c => c.Id == id && c.BusinessId == reminder.BusinessId, ct))
            return id;

        var existing = await db.Conversations
            .Where(c => c.ContactId == reminder.ContactId && c.BusinessId == reminder.BusinessId && c.ChannelType == reminder.Contact.ChannelType)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (existing is int found) return found;

        var conversation = new Conversation
        {
            BusinessId  = reminder.BusinessId,
            ContactId   = reminder.ContactId,
            ChannelType = reminder.Contact.ChannelType,
        };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(ct);
        return conversation.Id;
    }
}
