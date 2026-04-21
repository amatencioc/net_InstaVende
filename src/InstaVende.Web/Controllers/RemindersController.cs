using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Controllers;

[Authorize]
public class RemindersController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _user;

    public RemindersController(AppDbContext db, CurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<IActionResult> Index()
    {
        var biz = await _user.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Index", "Dashboard");

        var reminders = await _db.Reminders
            .Include(r => r.Contact)
            .Where(r => r.BusinessId == biz.Id)
            .OrderBy(r => r.ScheduledAt)
            .ToListAsync();

        var contacts = await _db.Contacts
            .Where(c => c.BusinessId == biz.Id)
            .Select(c => new { c.Id, c.Name, c.Phone })
            .ToListAsync();

        ViewBag.Contacts = contacts;
        return View(reminders.Select(r => MapToVm(r)).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] ReminderViewModel vm)
    {
        var biz = await _user.GetBusinessAsync();
        if (biz == null) return Json(new { ok = false });

        var userId = _user.GetUserId();

        Reminder entity;
        if (vm.Id == 0)
        {
            entity = new Reminder
            {
                BusinessId = biz.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedByAgentId = userId
            };
            _db.Reminders.Add(entity);
        }
        else
        {
            entity = await _db.Reminders.FirstOrDefaultAsync(r => r.Id == vm.Id && r.BusinessId == biz.Id);
                if (entity == null) return Json(new { ok = false });
        }

        entity.ContactId = vm.ContactId;
        entity.ConversationId = vm.ConversationId;
        entity.Message = vm.Message;
        entity.ChannelType = vm.ChannelType;
        entity.ScheduledAt = vm.ScheduledAt.ToUniversalTime();
        entity.TemplateKey = vm.TemplateKey;

        await _db.SaveChangesAsync();
        return Json(new { ok = true, id = entity.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var biz = await _user.GetBusinessAsync();
        var entity = await _db.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.BusinessId == biz!.Id);
        if (entity == null) return Json(new { ok = false });
        entity.Status = ReminderStatus.Cancelled;
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var biz = await _user.GetBusinessAsync();
        var entity = await _db.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.BusinessId == biz!.Id);
        if (entity == null) return Json(new { ok = false });
        _db.Reminders.Remove(entity);
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    private static ReminderViewModel MapToVm(Reminder r) => new()
    {
        Id = r.Id, ContactId = r.ContactId,
        ContactName = r.Contact?.Name ?? r.Contact?.Phone,
        ConversationId = r.ConversationId,
        Message = r.Message, ChannelType = r.ChannelType,
        Status = r.Status, ScheduledAt = r.ScheduledAt.ToLocalTime(),
        SentAt = r.SentAt?.ToLocalTime(), TemplateKey = r.TemplateKey,
        CreatedAt = r.CreatedAt
    };
}
