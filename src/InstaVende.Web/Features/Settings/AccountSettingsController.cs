using InstaVende.Core.Entities;
using InstaVende.Web.Controllers;
using InstaVende.Web.Infrastructure;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InstaVende.Web.Features.Settings;

/// <summary>
/// Perfil y seguridad de la cuenta del usuario autenticado.
/// Todo cambio que rota el security stamp (contraseña, email, cerrar sesiones) refresca la cookie
/// de la sesión actual y reemite el token CSRF para que el SPA siga funcionando sin volver a iniciar sesión.
/// </summary>
[Route("api/settings")]
public sealed class AccountSettingsController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ImageService images,
    CsrfTokenIssuer csrf,
    ILogger<AccountSettingsController> logger) : TenantController
{
    // ── Perfil ───────────────────────────────────────────────────────────────

    [HttpGet("profile")]
    [ProducesResponseType<SettingsProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SettingsProfileDto>> GetProfile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        return ToDto(user);
    }

    [HttpPut("profile")]
    [ProducesResponseType<SettingsProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SettingsProfileDto>> UpdateProfile(SettingsProfileUpdateRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        if (firstName.Length == 0) ModelState.AddModelError(nameof(request.FirstName), "El nombre es obligatorio.");
        if (lastName.Length == 0) ModelState.AddModelError(nameof(request.LastName), "El apellido es obligatorio.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        user.FirstName = firstName;
        user.LastName = lastName;
        // Asignación directa: SetPhoneNumberAsync rotaría el security stamp y cerraría las demás sesiones
        user.PhoneNumber = NullIfBlank(request.PhoneNumber);
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return IdentityProblem(result);
        return ToDto(user);
    }

    [HttpPost("profile/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageService.MaxBytes + 64 * 1024)]
    [ProducesResponseType<SettingsImageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SettingsImageDto>> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var upload = await images.SaveAsync(file, BusinessId, "avatars", ct);
        if (!upload.Success) return BadRequestProblem(upload.Error!);

        var previous = user.AvatarUrl;
        user.AvatarUrl = upload.Url;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            images.Delete(upload.Url);
            return IdentityProblem(result);
        }
        images.Delete(previous);
        return new SettingsImageDto(user.AvatarUrl);
    }

    [HttpDelete("profile/avatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAvatar()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        if (user.AvatarUrl == null) return NoContent();

        var previous = user.AvatarUrl;
        user.AvatarUrl = null;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return IdentityProblem(result);
        images.Delete(previous);
        return NoContent();
    }

    // ── Seguridad ────────────────────────────────────────────────────────────

    /// <summary>Cambia la contraseña. Las demás sesiones quedan invalidadas (rota el security stamp).</summary>
    [HttpPost("password"), EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(SettingsChangePasswordRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (request.CurrentPassword == request.NewPassword)
        {
            ModelState.AddModelError(nameof(request.NewPassword), "La nueva contraseña debe ser distinta de la actual.");
            return ValidationProblem(ModelState);
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)
                    ? nameof(request.CurrentPassword) : nameof(request.NewPassword), e.Description);
            return ValidationProblem(ModelState);
        }

        logger.LogInformation("Usuario {UserId} cambió su contraseña", user.Id);
        await RefreshSessionAsync(user);
        return NoContent();
    }

    /// <summary>Cambia el email de acceso (también es el nombre de usuario). Exige la contraseña actual.</summary>
    [HttpPost("email"), EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<SettingsProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SettingsProfileDto>> ChangeEmail(SettingsChangeEmailRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            ModelState.AddModelError(nameof(request.CurrentPassword), "La contraseña no es correcta.");
            return ValidationProblem(ModelState);
        }

        var newEmail = request.NewEmail.Trim();
        if (string.Equals(userManager.NormalizeEmail(newEmail), user.NormalizedEmail, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(request.NewEmail), "Ese ya es tu email actual.");
            return ValidationProblem(ModelState);
        }
        if (await userManager.FindByEmailAsync(newEmail) != null || await userManager.FindByNameAsync(newEmail) != null)
            return ConflictProblem("Ya existe una cuenta con ese email.");

        var oldUserName = user.UserName;
        var byName = await userManager.SetUserNameAsync(user, newEmail);
        if (!byName.Succeeded) return EmailProblem(byName);

        var byEmail = await userManager.SetEmailAsync(user, newEmail);
        if (!byEmail.Succeeded)
        {
            // Deja la cuenta coherente: email y nombre de usuario siempre iguales
            var rollback = await userManager.SetUserNameAsync(user, oldUserName);
            if (!rollback.Succeeded)
                logger.LogError("No se pudo revertir el nombre de usuario de {UserId} tras fallar el cambio de email", user.Id);
            return EmailProblem(byEmail);
        }

        logger.LogInformation("Usuario {UserId} cambió su email de acceso", user.Id);
        await RefreshSessionAsync(user);
        return ToDto(user);
    }

    /// <summary>
    /// Cierra la sesión en todos los demás dispositivos rotando el security stamp; la sesión actual se renueva.
    /// Las otras cookies dejan de ser válidas en la siguiente validación del stamp.
    /// </summary>
    [HttpPost("sessions/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeOtherSessions()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var result = await userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) return IdentityProblem(result);

        logger.LogInformation("Usuario {UserId} cerró sus sesiones en otros dispositivos", user.Id);
        await RefreshSessionAsync(user);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Reemite la cookie con el nuevo security stamp y un token CSRF ligado a la identidad renovada.</summary>
    private async Task RefreshSessionAsync(ApplicationUser user)
    {
        await signInManager.RefreshSignInAsync(user);
        HttpContext.User = await signInManager.CreateUserPrincipalAsync(user);
        csrf.Issue(HttpContext);
    }

    private ActionResult EmailProblem(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            return ConflictProblem("Ya existe una cuenta con ese email.");
        foreach (var e in result.Errors) ModelState.AddModelError("NewEmail", e.Description);
        return ValidationProblem(ModelState);
    }

    private ActionResult IdentityProblem(IdentityResult result)
        => BadRequestProblem(result.Errors.FirstOrDefault()?.Description ?? "No se pudieron guardar los cambios.");

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SettingsProfileDto ToDto(ApplicationUser user)
        => new(user.Email ?? string.Empty, user.FirstName, user.LastName, user.PhoneNumber, user.AvatarUrl, user.CreatedAt, user.LastLoginAt);
}
