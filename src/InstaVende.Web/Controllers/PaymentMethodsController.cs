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
public class PaymentMethodsController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _user;

    public PaymentMethodsController(AppDbContext db, CurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<IActionResult> Index()
    {
        var biz = await _user.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Index", "Dashboard");

        var methods = await _db.PaymentMethods
            .Where(p => p.BusinessId == biz.Id)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();

        ViewBag.BusinessName = biz.Name;
        var vms = methods.Select(m => MapToVm(m)).ToList();
        return View(vms);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] PaymentMethodViewModel vm)
    {
        var biz = await _user.GetBusinessAsync();
        if (biz == null) return Json(new { ok = false });

        PaymentMethod entity;
        if (vm.Id == 0)
        {
            entity = new PaymentMethod { BusinessId = biz.Id, CreatedAt = DateTime.UtcNow };
            _db.PaymentMethods.Add(entity);
        }
        else
        {
            entity = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == vm.Id && p.BusinessId == biz.Id);
                if (entity == null) return Json(new { ok = false });
        }

        entity.Type = vm.Type;
        entity.Name = vm.Name;
        entity.Instructions = vm.Instructions;
        entity.AccountAlias = vm.AccountAlias;
        entity.AccountNumber = vm.AccountNumber;
        entity.PaymentLink = vm.PaymentLink;
        entity.IsActive = vm.IsActive;
        entity.SortOrder = vm.SortOrder;

        await _db.SaveChangesAsync();
        await UpdateOnboarding(biz.Id);
        return Json(new { ok = true, id = entity.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var biz = await _user.GetBusinessAsync();
        var entity = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == biz!.Id);
        if (entity == null) return Json(new { ok = false });
        _db.PaymentMethods.Remove(entity);
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var biz = await _user.GetBusinessAsync();
        var entity = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == biz!.Id);
        if (entity == null) return Json(new { ok = false });
        entity.IsActive = !entity.IsActive;
        await _db.SaveChangesAsync();
        return Json(new { ok = true, isActive = entity.IsActive });
    }

    private static PaymentMethodViewModel MapToVm(PaymentMethod m) => new()
    {
        Id = m.Id, Type = m.Type, Name = m.Name,
        Instructions = m.Instructions, AccountAlias = m.AccountAlias,
        AccountNumber = m.AccountNumber, PaymentLink = m.PaymentLink,
        IsActive = m.IsActive, SortOrder = m.SortOrder
    };

    private async Task UpdateOnboarding(int businessId)
    {
        var prog = await _db.OnboardingProgresses.FirstOrDefaultAsync(o => o.BusinessId == businessId);
        if (prog != null)
        {
            prog.PaymentMethodsAdded = true;
            prog.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
