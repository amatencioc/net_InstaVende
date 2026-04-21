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
public class ReminderTemplatesController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;

    private static readonly Dictionary<CustomerSegment, (int order, string message, string window)[]> DefaultTemplates = new()
    {
        [CustomerSegment.Frio] =
        [
            (1, "Hola ??, gracias por contactarnos. ¿En qué puedo ayudarte hoy?", "2-3h")
        ],
        [CustomerSegment.Tibio] =
        [
            (1, "Gracias por escribirnos ?? ¿Tienes alguna duda sobre nuestros productos?", "2-3h"),
            (2, "¡Hola! Ayer hablamos un poquito. ¿Te animas a revisar de nuevo?", "22-23h")
        ],
        [CustomerSegment.Caliente] =
        [
            (1, "Gracias por tu interés ?? Estás a un paso de tener tu pedido listo.", "2-3h"),
            (2, "¡Hola! Solo queríamos recordarte que tu pedido sigue apartado ??", "22-23h")
        ]
    };

    public ReminderTemplatesController(AppDbContext db, CurrentUserService cu)
    {
        _db = db;
        _cu = cu;
    }

    public async Task<IActionResult> Index(CustomerSegment segment = CustomerSegment.Frio)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Register", "Account");

        await EnsureDefaultTemplates(biz.Id);

        var templates = await _db.ReminderTemplates
            .Where(t => t.BusinessId == biz.Id && t.Segment == segment)
            .OrderBy(t => t.Order)
            .ToListAsync();

        var vm = new ReminderSegmentViewModel
        {
            Segment = segment,
            ActiveSegment = segment,
            Reminders = templates.Select(t => new ReminderTemplateViewModel
            {
                Id = t.Id,
                Segment = t.Segment,
                Order = t.Order,
                Message = t.Message,
                TimeWindow = t.TimeWindow,
                IsActive = t.IsActive,
                MediaUrl = t.MediaUrl
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] List<ReminderTemplateViewModel> models)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();

        foreach (var model in models)
        {
            var t = await _db.ReminderTemplates.FirstOrDefaultAsync(r => r.Id == model.Id && r.BusinessId == biz.Id);
            if (t == null) continue;
            t.Message = model.Message;
            t.IsActive = model.IsActive;
            t.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle([FromBody] IdToggleRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        var t = await _db.ReminderTemplates.FirstOrDefaultAsync(r => r.Id == model.Id && r.BusinessId == biz.Id);
        if (t == null) return NotFound();
        t.IsActive = !t.IsActive;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Json(new { success = true, isActive = t.IsActive });
    }

    private async Task EnsureDefaultTemplates(int bizId)
    {
        foreach (var (segment, defaults) in DefaultTemplates)
        {
            foreach (var (order, message, window) in defaults)
            {
                var exists = await _db.ReminderTemplates
                    .AnyAsync(t => t.BusinessId == bizId && t.Segment == segment && t.Order == order);
                if (!exists)
                {
                    _db.ReminderTemplates.Add(new ReminderTemplate
                    {
                        BusinessId = bizId,
                        Segment = segment,
                        Order = order,
                        Message = message,
                        TimeWindow = window,
                        IsActive = true
                    });
                }
            }
        }
        await _db.SaveChangesAsync();
    }
}

public class IdToggleRequest { public int Id { get; set; } }
