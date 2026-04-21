using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InstaVende.Core.Entities;

namespace InstaVende.Web.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _user;
    private readonly UserManager<ApplicationUser> _userManager;

    public SettingsController(AppDbContext db, CurrentUserService user, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _user = user;
        _userManager = userManager;
    }

    public async Task<IActionResult> Account()
    {
        var appUser = await _userManager.GetUserAsync(User);
        if (appUser == null) return RedirectToAction("Login", "Account");

        var vm = new AccountProfileViewModel
        {
            FirstName = appUser.FirstName,
            LastName = appUser.LastName,
            Email = appUser.Email ?? string.Empty,
            AvatarUrl = appUser.AvatarUrl
        };
        ViewData["Title"] = "Mi perfil";
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Account(AccountProfileViewModel vm)
    {
        if (!ModelState.IsValid) { ViewData["Title"] = "Mi perfil"; return View(vm); }

        var appUser = await _userManager.GetUserAsync(User);
        if (appUser == null) return RedirectToAction("Login", "Account");

        appUser.FirstName = vm.FirstName;
        appUser.LastName = vm.LastName;

        if (appUser.Email != vm.Email)
        {
            var setEmailResult = await _userManager.SetEmailAsync(appUser, vm.Email);
            if (!setEmailResult.Succeeded)
            {
                foreach (var e in setEmailResult.Errors) ModelState.AddModelError("", e.Description);
                ViewData["Title"] = "Mi perfil";
                return View(vm);
            }
            await _userManager.SetUserNameAsync(appUser, vm.Email);
        }

        await _userManager.UpdateAsync(appUser);
        TempData["Success"] = "Perfil actualizado correctamente.";
        return RedirectToAction(nameof(Account));
    }

    public async Task<IActionResult> Business()
    {
        var biz = await _user.GetBusinessAsync();
        ViewData["Title"] = "Perfil del negocio";
        if (biz == null) return View(new BusinessProfileViewModel());

        var vm = new BusinessProfileViewModel
        {
            Name = biz.Name, Description = biz.Description,
            Sector = biz.Sector, Phone = biz.Phone, Email = biz.Email,
            WebsiteUrl = biz.WebsiteUrl, WhatsAppNumber = biz.WhatsAppNumber,
            LogoUrl = biz.LogoUrl
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Business(BusinessProfileViewModel vm)
    {
        ViewData["Title"] = "Perfil del negocio";
        if (!ModelState.IsValid) return View(vm);

        var biz = await _user.GetBusinessAsync();
        if (biz == null) return View(vm);

        biz.Name = vm.Name;
        biz.Description = vm.Description;
        biz.Sector = vm.Sector;
        biz.Phone = vm.Phone;
        biz.Email = vm.Email;
        biz.WebsiteUrl = vm.WebsiteUrl;
        biz.WhatsAppNumber = vm.WhatsAppNumber;
        biz.LogoUrl = vm.LogoUrl;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Perfil del negocio actualizado.";
        return RedirectToAction(nameof(Business));
    }

    public IActionResult Password()
    {
        ViewData["Title"] = "Cambiar contraseña";
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Password(ChangePasswordViewModel vm)
    {
        ViewData["Title"] = "Cambiar contraseña";
        if (!ModelState.IsValid) return View(vm);

        var appUser = await _userManager.GetUserAsync(User);
        if (appUser == null) return RedirectToAction("Login", "Account");

        var result = await _userManager.ChangePasswordAsync(appUser, vm.CurrentPassword, vm.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        TempData["Success"] = "Contraseña actualizada correctamente.";
        return RedirectToAction(nameof(Account));
    }
}
