using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Hubs;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Controllers;

[Authorize]
public class InboxController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;
    private readonly IEnumerable<IChannelMessageSender> _senders;
    private readonly IHubContext<InboxHub> _hub;

    public InboxController(AppDbContext db, CurrentUserService cu, IEnumerable<IChannelMessageSender> senders, IHubContext<InboxHub> hub)
    { _db = db; _cu = cu; _senders = senders; _hub = hub; }

    public async Task<IActionResult> Index()
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Register", "Account");
        ViewBag.BusinessId = biz.Id;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations(ChannelType? channel = null, ConversationStatus? status = null)
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();
        var q = _db.Conversations.Include(c => c.Contact).Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1)).Where(c => c.BusinessId == bid);
        if (channel.HasValue) q = q.Where(c => c.ChannelType == channel.Value);
        if (status.HasValue) q = q.Where(c => c.Status == status.Value);
        var list = await q.OrderByDescending(c => c.UpdatedAt).Take(100).ToListAsync();
        return Json(list.Select(c => new ConversationListItemViewModel
        {
            Id = c.Id, ContactName = c.Contact.Name ?? c.Contact.ExternalId, ContactPhone = c.Contact.Phone,
            AvatarUrl = c.Contact.AvatarUrl, Channel = c.ChannelType, Status = c.Status,
            LastMessage = c.Messages.FirstOrDefault()?.Content, UpdatedAt = c.UpdatedAt,
            UnreadCount = c.Messages.Count(m => !m.IsRead && m.Direction == MessageDirection.Inbound)
        }));
    }

    [HttpGet]
    public async Task<IActionResult> GetMessages(int conversationId)
    {
        var bid = await _cu.GetBusinessIdAsync();
        var conv = await _db.Conversations.Include(c => c.Messages.OrderBy(m => m.SentAt))
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.BusinessId == bid);
        if (conv == null) return NotFound();
        foreach (var m in conv.Messages.Where(m => !m.IsRead && m.Direction == MessageDirection.Inbound)) m.IsRead = true;
        await _db.SaveChangesAsync();
        return Json(conv.Messages.Select(m => new MessageViewModel { Id = m.Id, Content = m.Content, MediaUrl = m.MediaUrl, Direction = m.Direction, SentByBot = m.SentByBot, SentAt = m.SentAt }));
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        var conv = await _db.Conversations.Include(c => c.Contact).FirstOrDefaultAsync(c => c.Id == model.ConversationId && c.BusinessId == bid);
        if (conv == null) return NotFound();
        var msg = new Message { ConversationId = conv.Id, Direction = MessageDirection.Outbound, Content = model.Message, IsRead = true, SentByBot = false };
        _db.Messages.Add(msg); conv.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync();
        var sender = _senders.FirstOrDefault(s => s.Channel == conv.ChannelType);
        if (sender != null) await sender.SendTextAsync(bid!.Value, conv.Contact.ExternalId, model.Message);
        await _hub.Clients.Group($"business_{bid}").SendAsync("NewMessage", new { conversationId = conv.Id, messageId = msg.Id, content = msg.Content, direction = (int)msg.Direction, sentAt = msg.SentAt });
        return Json(new { success = true, messageId = msg.Id });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest req)
    {
        var bid = await _cu.GetBusinessIdAsync();
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == req.ConversationId && c.BusinessId == bid);
        if (conv == null) return NotFound();
        conv.Status = req.Status;
        if (req.Status == ConversationStatus.Resolved) conv.ResolvedAt = DateTime.UtcNow;
        conv.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync();
        return Json(new { success = true });
    }
}

public record UpdateStatusRequest(int ConversationId, ConversationStatus Status);
