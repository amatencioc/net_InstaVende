using InstaVende.Core.Entities;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InstaVende.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;
    private readonly ILogger<AccountController> _logger;

    public AccountController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, AppDbContext db,
        CurrentUserService cu, ILogger<AccountController> logger)
    { _userManager = um; _signInManager = sm; _db = db; _cu = cu; _logger = logger; }

    [HttpGet]
    public IActionResult Register()
        => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : View();

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(), Email = model.Email.Trim(),
            FirstName = model.FirstName.Trim(), LastName = model.LastName.Trim()
        };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        if (!await TryCreateBusinessAsync(user, model.BusinessName))
        {
            // Sin negocio el usuario quedaría huérfano: se elimina para que pueda reintentar.
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "Error al crear la cuenta. Intenta de nuevo.");
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToLocal(returnUrl);
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToLocal(returnUrl);
        }
        if (result.IsLockedOut) return View("Lockout");

        ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
        return View(model);
    }

    /// <summary>
    /// Destino de los usuarios autenticados sin negocio (p.ej. invitados aún no vinculados
    /// o registros incompletos). Evita el bucle Dashboard → Register → Dashboard.
    /// </summary>
    [Authorize, HttpGet]
    public async Task<IActionResult> SetupBusiness()
    {
        if (await _cu.GetBusinessIdAsync() != null) return RedirectToAction("Index", "Dashboard");
        return View(new SetupBusinessViewModel());
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetupBusiness(SetupBusinessViewModel model)
    {
        if (await _cu.GetBusinessIdAsync() != null) return RedirectToAction("Index", "Dashboard");
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        if (!await TryCreateBusinessAsync(user, model.BusinessName))
        {
            ModelState.AddModelError(string.Empty, "No se pudo crear el negocio. Intenta de nuevo.");
            return View(model);
        }
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    private async Task<bool> TryCreateBusinessAsync(ApplicationUser user, string businessName)
    {
        try
        {
            if (!await _userManager.IsInRoleAsync(user, "Merchant"))
                await _userManager.AddToRoleAsync(user, "Merchant");
            _db.Businesses.Add(new Business { UserId = user.Id, Name = businessName.Trim() });
            await _db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo crear el negocio para el usuario {UserId}", user.Id);
            // Limpia la entidad Business pendiente para que DeleteAsync no reintente el INSERT fallido.
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    private IActionResult RedirectToLocal(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Dashboard");
}
