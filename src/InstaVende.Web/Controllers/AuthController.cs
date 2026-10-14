using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Infrastructure;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InstaVende.Web.Controllers;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record SessionDto(SessionUserDto User, SessionBusinessDto? Business);

public sealed record SessionUserDto(string Id, string Email, string FirstName, string LastName, string? AvatarUrl);

public sealed record SessionBusinessDto(int Id, string Name, string? Country, string? Currency, string CurrencySymbol,
    string TimeZone, UserRole Role);

public sealed record LoginRequest(
    [property: Required(ErrorMessage = "El email es obligatorio."), EmailAddress(ErrorMessage = "Ingresa un email válido.")] string Email,
    [property: Required(ErrorMessage = "La contraseña es obligatoria.")] string Password,
    bool RememberMe = false);

public sealed record RegisterRequest(
    [property: Required(ErrorMessage = "El nombre es obligatorio."), StringLength(100)] string FirstName,
    [property: Required(ErrorMessage = "El apellido es obligatorio."), StringLength(100)] string LastName,
    [property: Required(ErrorMessage = "El email es obligatorio."), EmailAddress(ErrorMessage = "Ingresa un email válido.")] string Email,
    [property: Required(ErrorMessage = "La contraseña es obligatoria."), StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")] string Password,
    [property: Required(ErrorMessage = "El nombre del negocio es obligatorio."), StringLength(200)] string BusinessName);

public sealed record SetupBusinessRequest(
    [property: Required(ErrorMessage = "El nombre del negocio es obligatorio."), StringLength(200)] string BusinessName);

/// <summary>Autenticación del SPA con cookie HttpOnly + token CSRF (cookie XSRF-TOKEN / cabecera X-XSRF-TOKEN).</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AppDbContext db,
    CurrentUserService currentUser,
    CsrfTokenIssuer csrf,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>Emite la cookie XSRF-TOKEN. El SPA la llama al arrancar.</summary>
    [HttpGet("csrf"), AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Csrf()
    {
        csrf.Issue(HttpContext);
        return NoContent();
    }

    /// <summary>Sesión actual (usuario y negocio). 401 si no hay sesión.</summary>
    [HttpGet("session"), Authorize]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionDto>> Session()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        return await BuildSessionAsync(user);
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<ActionResult<SessionDto>> Login(LoginRequest request)
    {
        var email = request.Email.Trim();
        var result = await signInManager.PasswordSignInAsync(email, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return Problem(statusCode: StatusCodes.Status423Locked,
                detail: "Tu cuenta quedó bloqueada temporalmente por varios intentos fallidos. Inténtalo en unos minutos.");
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Email o contraseña incorrectos.");

        var user = (await userManager.FindByEmailAsync(email))!;
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        await RefreshPrincipalAsync(user);
        return await BuildSessionAsync(user);
    }

    [HttpPost("register"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionDto>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(), Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(),
        };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            foreach (var e in created.Errors) ModelState.AddModelError(e.Code.Contains("Password") ? nameof(request.Password) : nameof(request.Email), e.Description);
            return ValidationProblem(ModelState);
        }

        if (!await TryCreateBusinessAsync(user, request.BusinessName))
        {
            await userManager.DeleteAsync(user);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: "No se pudo crear la cuenta. Intenta de nuevo.");
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        await RefreshPrincipalAsync(user);
        return await BuildSessionAsync(user);
    }

    /// <summary>Crea el negocio de un usuario autenticado que no tiene ninguno.</summary>
    [HttpPost("setup-business"), Authorize]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SessionDto>> SetupBusiness(SetupBusinessRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        if (await currentUser.GetBusinessIdAsync() != null)
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: "Tu usuario ya tiene un negocio.");
        if (!await TryCreateBusinessAsync(user, request.BusinessName))
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: "No se pudo crear el negocio. Intenta de nuevo.");
        return await BuildSessionAsync(user);
    }

    [HttpPost("logout"), Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        csrf.Issue(HttpContext);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>El token CSRF está ligado a la identidad: se reemite tras iniciar sesión.</summary>
    private async Task RefreshPrincipalAsync(ApplicationUser user)
    {
        HttpContext.User = await signInManager.CreateUserPrincipalAsync(user);
        csrf.Issue(HttpContext);
    }

    private async Task<SessionDto> BuildSessionAsync(ApplicationUser user)
    {
        var tenant = await currentUser.GetTenantAsync(user.Id);
        return new SessionDto(
            new SessionUserDto(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, user.AvatarUrl),
            tenant == null ? null : new SessionBusinessDto(tenant.BusinessId, tenant.BusinessName, tenant.Country, tenant.Currency,
                tenant.CurrencySymbol, tenant.TimeZone.Id, tenant.Role));
    }

    private async Task<bool> TryCreateBusinessAsync(ApplicationUser user, string businessName)
    {
        try
        {
            if (!await userManager.IsInRoleAsync(user, AppRoles.Merchant))
                await userManager.AddToRoleAsync(user, AppRoles.Merchant);
            db.Businesses.Add(new Business { UserId = user.Id, Name = businessName.Trim() });
            await db.SaveChangesAsync();
            currentUser.Reset();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo crear el negocio para el usuario {UserId}", user.Id);
            db.ChangeTracker.Clear(); // evita que DeleteAsync reintente el INSERT fallido
            return false;
        }
    }
}
