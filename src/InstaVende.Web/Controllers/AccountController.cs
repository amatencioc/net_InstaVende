using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InstaVende.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;

    public AccountController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, AppDbContext db)
    { _userManager = um; _signInManager = sm; _db = db; }

    [HttpGet] public IActionResult Register()
        => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : View();

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = new ApplicationUser { UserName = model.Email, Email = model.Email, FirstName = model.FirstName, LastName = model.LastName };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                await _userManager.AddToRoleAsync(user, "Merchant");
                _db.Businesses.Add(new Business { UserId = user.Id, Name = model.BusinessName });
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                await _userManager.DeleteAsync(user);
                ModelState.AddModelError(string.Empty, "Error al crear la cuenta. Intenta de nuevo.");
                return View(model);
            }
            await _signInManager.SignInAsync(user, false);
            return RedirectToAction("Index", "Dashboard");
        }
        foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
        return View(model);
    }

    [HttpGet] public IActionResult Login(string? returnUrl = null) { ViewData["ReturnUrl"] = returnUrl; return View(); }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded) return LocalRedirect(returnUrl ?? Url.Action("Index", "Dashboard")!);
        if (result.IsLockedOut) return View("Lockout");
        ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
        return View(model);
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await _signInManager.SignOutAsync(); return RedirectToAction("Login"); }
}
