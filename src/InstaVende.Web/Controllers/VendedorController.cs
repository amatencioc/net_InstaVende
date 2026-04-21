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
public class VendedorController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;
    private readonly ImageService _img;

    public VendedorController(AppDbContext db, CurrentUserService cu, ImageService img)
    {
        _db = db;
        _cu = cu;
        _img = img;
    }

    // ?? Personalidad ?????????????????????????????????????????????????????????
    public async Task<IActionResult> Personalidad()
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Register", "Account");

        var config = await _db.VendorConfigs.FirstOrDefaultAsync(v => v.BusinessId == biz.Id);
        var vm = config == null
            ? new VendorPersonalidadViewModel { BusinessName = biz.Name }
            : new VendorPersonalidadViewModel
            {
                Id = config.Id,
                VendorName = config.VendorName,
                VendorGender = config.VendorGender,
                BusinessName = biz.Name,
                Country = config.Country,
                BusinessDescription = config.BusinessDescription,
                TargetAudience = config.TargetAudience,
                Rules = config.Rules,
                CommunicationStyle = config.CommunicationStyle,
                SalesStyle = config.SalesStyle,
                ResponseLength = config.ResponseLength,
                UseEmojis = config.UseEmojis,
                UseOpeningPunctuation = config.UseOpeningPunctuation,
                WordsToAvoid = config.WordsToAvoid,
                EmojiPalette = config.EmojiPalette,
                WelcomeMessage = config.WelcomeMessage,
                WelcomeMediaUrl = config.WelcomeMediaUrl,
                PurchaseConfirmationMessage = config.PurchaseConfirmationMessage,
                HumanHandoffSituations = config.HumanHandoffSituations,
                AutoPauseOnHandoff = config.AutoPauseOnHandoff,
                HandoffExampleMessage = config.HandoffExampleMessage
            };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePersonalidad([FromBody] VendorPersonalidadViewModel model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();

        var config = await _db.VendorConfigs.FirstOrDefaultAsync(v => v.BusinessId == biz.Id);
        if (config == null)
        {
            config = new VendorConfig { BusinessId = biz.Id };
            _db.VendorConfigs.Add(config);
        }

        config.VendorName = model.VendorName;
        config.VendorGender = model.VendorGender;
        config.Country = model.Country;
        config.BusinessDescription = model.BusinessDescription;
        config.TargetAudience = model.TargetAudience;
        config.Rules = model.Rules;
        config.CommunicationStyle = model.CommunicationStyle;
        config.SalesStyle = model.SalesStyle;
        config.ResponseLength = model.ResponseLength;
        config.UseEmojis = model.UseEmojis;
        config.UseOpeningPunctuation = model.UseOpeningPunctuation;
        config.WordsToAvoid = model.WordsToAvoid;
        config.EmojiPalette = model.EmojiPalette;
        config.WelcomeMessage = model.WelcomeMessage;
        config.PurchaseConfirmationMessage = model.PurchaseConfirmationMessage;
        config.HumanHandoffSituations = model.HumanHandoffSituations;
        config.AutoPauseOnHandoff = model.AutoPauseOnHandoff;
        config.HandoffExampleMessage = model.HandoffExampleMessage;
        config.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ?? Base de Conocimiento ?????????????????????????????????????????????????
    public async Task<IActionResult> BaseConocimiento(KnowledgeCategory? categoria)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Register", "Account");

        var q = _db.KnowledgeEntries.Where(k => k.BusinessId == biz.Id);
        if (categoria.HasValue) q = q.Where(k => k.Category == categoria.Value);

        var entries = await q.OrderByDescending(k => k.IsFavorite).ThenByDescending(k => k.CreatedAt).ToListAsync();

        var vm = new BaseConocimientoViewModel
        {
            FilterCategory = categoria,
            Entries = entries.Select(k => new KnowledgeEntryViewModel
            {
                Id = k.Id,
                Title = k.Title,
                Content = k.Content,
                Category = k.Category,
                IsFavorite = k.IsFavorite,
                CreatedAt = k.CreatedAt
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveKnowledge([FromBody] KnowledgeEntryViewModel model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();

        KnowledgeEntry? entry;
        if (model.Id > 0)
        {
            entry = await _db.KnowledgeEntries.FirstOrDefaultAsync(k => k.Id == model.Id && k.BusinessId == biz.Id);
            if (entry == null) return NotFound();
        }
        else
        {
            entry = new KnowledgeEntry { BusinessId = biz.Id };
            _db.KnowledgeEntries.Add(entry);
        }

        entry.Title = model.Title;
        entry.Content = model.Content;
        entry.Category = model.Category;
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Json(new { success = true, id = entry.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavoriteKnowledge([FromBody] IdOnlyRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        var entry = await _db.KnowledgeEntries.FirstOrDefaultAsync(k => k.Id == model.Id && k.BusinessId == biz.Id);
        if (entry == null) return NotFound();
        entry.IsFavorite = !entry.IsFavorite;
        await _db.SaveChangesAsync();
        return Json(new { success = true, isFavorite = entry.IsFavorite });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteKnowledge([FromBody] IdOnlyRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        var entry = await _db.KnowledgeEntries.FirstOrDefaultAsync(k => k.Id == model.Id && k.BusinessId == biz.Id);
        if (entry == null) return NotFound();
        _db.KnowledgeEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ?? Entrega ??????????????????????????????????????????????????????????????
    public async Task<IActionResult> Entrega()
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Register", "Account");

        var zones = await _db.DeliveryZones
            .Where(d => d.BusinessId == biz.Id)
            .OrderBy(d => d.SortOrder)
            .ToListAsync();

        var vm = zones.Select(d => new DeliveryZoneViewModel
        {
            Id = d.Id,
            Name = d.Name,
            Cost = d.Cost,
            Description = d.Description,
            IsActive = d.IsActive,
            SortOrder = d.SortOrder
        }).ToList();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDeliveryZone([FromBody] DeliveryZoneViewModel model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();

        DeliveryZone? zone;
        if (model.Id > 0)
        {
            zone = await _db.DeliveryZones.FirstOrDefaultAsync(d => d.Id == model.Id && d.BusinessId == biz.Id);
            if (zone == null) return NotFound();
        }
        else
        {
            zone = new DeliveryZone { BusinessId = biz.Id };
            _db.DeliveryZones.Add(zone);
        }

        zone.Name = model.Name;
        zone.Cost = model.Cost;
        zone.Description = model.Description;
        zone.IsActive = model.IsActive;
        zone.SortOrder = model.SortOrder;
        await _db.SaveChangesAsync();
        return Json(new { success = true, id = zone.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDeliveryZone([FromBody] IdOnlyRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        var zone = await _db.DeliveryZones.FirstOrDefaultAsync(d => d.Id == model.Id && d.BusinessId == biz.Id);
        if (zone == null) return NotFound();
        _db.DeliveryZones.Remove(zone);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDeliveryZone([FromBody] IdOnlyRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        var zone = await _db.DeliveryZones.FirstOrDefaultAsync(d => d.Id == model.Id && d.BusinessId == biz.Id);
        if (zone == null) return NotFound();
        zone.IsActive = !zone.IsActive;
        await _db.SaveChangesAsync();
        return Json(new { success = true, isActive = zone.IsActive });
    }

    // ?? Pagos ????????????????????????????????????????????????????????????????
    public async Task<IActionResult> Pagos()
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Register", "Account");

        var methods = await _db.PaymentMethods.Where(p => p.BusinessId == biz.Id).OrderBy(p => p.SortOrder).ToListAsync();
        var images = await _db.PaymentImages.Where(p => p.BusinessId == biz.Id).OrderBy(p => p.SortOrder).ToListAsync();

        var vm = new VendedorPagoViewModel
        {
            PaymentMethods = methods.Select(m => new VendedorPaymentMethodViewModel
            {
                Id = m.Id,
                Name = m.Name,
                Type = m.Type.ToString(),
                Instructions = m.Instructions,
                IsActive = m.IsActive,
                SortOrder = m.SortOrder
            }).ToList(),
            PaymentImages = images.Select(i => new PaymentImageViewModel
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
                SortOrder = i.SortOrder
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePaymentMethod([FromBody] VendedorPaymentMethodViewModel model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();

        var pm = model.Id > 0
            ? await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == model.Id && p.BusinessId == biz.Id)
            : null;

        if (pm == null)
        {
            pm = new InstaVende.Core.Entities.PaymentMethod { BusinessId = biz.Id };
            _db.PaymentMethods.Add(pm);
        }

        pm.Name = model.Name;
        pm.Instructions = model.Instructions;
        pm.IsActive = model.IsActive;
        pm.SortOrder = model.SortOrder;
        await _db.SaveChangesAsync();
        return Json(new { success = true, id = pm.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePaymentMethod([FromBody] IdOnlyRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        var pm = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == model.Id && p.BusinessId == biz.Id);
        if (pm == null) return NotFound();
        _db.PaymentMethods.Remove(pm);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePaymentMethod([FromBody] IdOnlyRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        var pm = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == model.Id && p.BusinessId == biz.Id);
        if (pm == null) return NotFound();
        pm.IsActive = !pm.IsActive;
        await _db.SaveChangesAsync();
        return Json(new { success = true, isActive = pm.IsActive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPaymentImage(IFormFile file)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();

        var count = await _db.PaymentImages.CountAsync(p => p.BusinessId == biz.Id);
        if (count >= 5) return BadRequest(new { error = "Máximo 5 imágenes de pago." });
        if (file.Length > 5 * 1024 * 1024) return BadRequest(new { error = "El archivo supera los 5MB." });

        var url = await _img.SaveImageAsync(file, "payment-images");
        var img = new PaymentImage { BusinessId = biz.Id, ImageUrl = url, SortOrder = count };
        _db.PaymentImages.Add(img);
        await _db.SaveChangesAsync();
        return Json(new { success = true, id = img.Id, imageUrl = url });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePaymentImage([FromBody] IdOnlyRequest model)
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return Unauthorized();
        var img = await _db.PaymentImages.FirstOrDefaultAsync(i => i.Id == model.Id && i.BusinessId == biz.Id);
        if (img == null) return NotFound();
        _db.PaymentImages.Remove(img);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }
}

public class IdOnlyRequest { public int Id { get; set; } }
