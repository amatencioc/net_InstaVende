using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using InstaVende.Web.Infrastructure;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Settings;

/// <summary>
/// Enlace público de invitación (<c>/invite/{token}</c> en el SPA). El token se busca por su hash SHA-256.
/// Un usuario solo puede pertenecer a un negocio: los propietarios o miembros de otro negocio no pueden unirse.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/invitations")]
[Produces("application/json")]
public sealed class InvitationsController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    CurrentUserService currentUser,
    CsrfTokenIssuer csrf,
    IConfiguration configuration,
    ILogger<InvitationsController> logger) : ControllerBase
{
    private const string NotFoundMessage = "La invitación no existe o el enlace está incompleto. Revisa que lo hayas copiado entero.";

    [HttpGet("{token}")]
    [ProducesResponseType<InvitationDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvitationDetailsDto>> Get(string token, CancellationToken ct)
    {
        var invitation = await FindAsync(token, ct);
        if (invitation == null) return Problem(statusCode: StatusCodes.Status404NotFound, detail: NotFoundMessage);

        var status = StatusOf(invitation.Status, invitation.ExpiresAt);
        var valid = status == InvitationLinkStatus.Valid;
        var hasAccount = valid && await userManager.FindByEmailAsync(invitation.Email) != null;
        return new InvitationDetailsDto(status, invitation.BusinessName, invitation.BusinessLogoUrl,
            valid ? invitation.Email : null, invitation.Role, invitation.ExpiresAt, hasAccount);
    }

    /// <summary>
    /// Acepta la invitación. Con sesión iniciada, el email de la cuenta debe coincidir con el invitado.
    /// Sin sesión, crea la cuenta con el email invitado e inicia sesión. Devuelve la sesión resultante.
    /// </summary>
    [HttpPost("{token}/accept"), EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SessionDto>> Accept(string token, InvitationAcceptRequest request, CancellationToken ct)
    {
        var invitation = await FindAsync(token, ct);
        if (invitation == null) return Problem(statusCode: StatusCodes.Status404NotFound, detail: NotFoundMessage);

        switch (StatusOf(invitation.Status, invitation.ExpiresAt))
        {
            case InvitationLinkStatus.Accepted: return ConflictProblem("Esta invitación ya fue utilizada.");
            case InvitationLinkStatus.Cancelled: return ConflictProblem("El administrador canceló esta invitación.");
            case InvitationLinkStatus.Expired: return ConflictProblem("La invitación expiró. Pide al administrador un enlace nuevo.");
        }
        if (!invitation.BusinessActive) return ConflictProblem("El negocio que te invitó ya no está activo.");

        ApplicationUser user;
        string? password = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var current = await userManager.GetUserAsync(User);
            if (current == null) return Unauthorized();
            user = current;
            if (!string.Equals(userManager.NormalizeEmail(invitation.Email), user.NormalizedEmail, StringComparison.Ordinal))
                return Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: $"Esta invitación es para {invitation.Email}. Cierra sesión e ingresa con esa cuenta para aceptarla.");

            if (await db.Businesses.AnyAsync(b => b.UserId == user.Id, ct))
                return ConflictProblem("Tu cuenta ya es propietaria de un negocio en InstaVende y cada cuenta solo puede pertenecer a uno. " +
                                "Pide que te inviten con otro email para unirte a este equipo.");
            if (await db.BusinessUsers.AnyAsync(bu => bu.UserId == user.Id && bu.BusinessId == invitation.BusinessId, ct))
                return ConflictProblem("Ya formas parte de este equipo.");
            if (await db.BusinessUsers.AnyAsync(bu => bu.UserId == user.Id, ct))
                return ConflictProblem("Tu cuenta ya pertenece al equipo de otro negocio y cada cuenta solo puede pertenecer a uno. " +
                                "Pide que te inviten con otro email.");
        }
        else
        {
            var firstName = request.FirstName?.Trim() ?? string.Empty;
            var lastName = request.LastName?.Trim() ?? string.Empty;
            password = request.Password ?? string.Empty;
            if (firstName.Length == 0) ModelState.AddModelError(nameof(request.FirstName), "El nombre es obligatorio.");
            if (lastName.Length == 0) ModelState.AddModelError(nameof(request.LastName), "El apellido es obligatorio.");
            if (password.Length == 0) ModelState.AddModelError(nameof(request.Password), "La contraseña es obligatoria.");
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            if (await userManager.FindByEmailAsync(invitation.Email) != null)
                return ConflictProblem("Ya existe una cuenta con este email. Inicia sesión para aceptar la invitación.");

            user = new ApplicationUser
            {
                UserName = invitation.Email, Email = invitation.Email,
                FirstName = firstName, LastName = lastName,
            };
            // Se valida la contraseña antes de reservar la invitación para no tener que revertirla
            foreach (var validator in userManager.PasswordValidators)
            {
                var check = await validator.ValidateAsync(userManager, user, password);
                foreach (var e in check.Errors) ModelState.AddModelError(nameof(request.Password), e.Description);
            }
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
        }

        var maxUsers = TeamRules.MaxUsers(configuration);
        var members = 1 + await db.BusinessUsers.CountAsync(bu => bu.BusinessId == invitation.BusinessId, ct);
        if (members >= maxUsers)
            return ConflictProblem("El equipo alcanzó el límite de usuarios de su plan. Pide al administrador que libere un cupo.");

        // Reserva atómica: si dos peticiones aceptan a la vez, solo una gana
        var now = DateTime.UtcNow;
        var claimed = await db.UserInvitations
            .Where(i => i.Id == invitation.Id && i.Status == InvitationStatus.Pending && i.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, InvitationStatus.Accepted), ct);
        if (claimed == 0) return ConflictProblem("Esta invitación ya fue utilizada.");

        var created = false;
        try
        {
            if (password != null)
            {
                var result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    await ReleaseAsync(invitation.Id);
                    foreach (var e in result.Errors)
                        ModelState.AddModelError(e.Code.Contains("Password") ? nameof(request.Password) : "Email", e.Description);
                    return ValidationProblem(ModelState);
                }
                created = true;
                await TryAddMemberRoleAsync(user);
            }

            db.BusinessUsers.Add(new BusinessUser
            {
                BusinessId = invitation.BusinessId, UserId = user.Id, Role = invitation.Role, JoinedAt = now,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "No se pudo completar la invitación {InvitationId}", invitation.Id);
            db.ChangeTracker.Clear();
            if (created) await userManager.DeleteAsync(user);
            await ReleaseAsync(invitation.Id);
            return ConflictProblem("No se pudo completar la invitación. Inténtalo de nuevo.");
        }

        if (created)
        {
            await signInManager.SignInAsync(user, isPersistent: false);
            user.LastLoginAt = DateTime.UtcNow;
            await userManager.UpdateAsync(user);
            // El token CSRF está ligado a la identidad: se reemite para la nueva sesión
            HttpContext.User = await signInManager.CreateUserPrincipalAsync(user);
            csrf.Issue(HttpContext);
        }

        logger.LogInformation("Usuario {UserId} se unió al negocio {BusinessId} con rol {Role}", user.Id, invitation.BusinessId, invitation.Role);
        currentUser.Reset();
        var tenant = await currentUser.GetTenantAsync(user.Id);
        return new SessionDto(
            new SessionUserDto(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, user.AvatarUrl),
            tenant == null ? null : new SessionBusinessDto(tenant.BusinessId, tenant.BusinessName, tenant.Country, tenant.Currency,
                tenant.CurrencySymbol, tenant.TimeZone.Id, tenant.Role));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed record InvitationRow(int Id, int BusinessId, string Email, UserRole Role, InvitationStatus Status,
        DateTime ExpiresAt, string BusinessName, string? BusinessLogoUrl, bool BusinessActive);

    private async Task<InvitationRow?> FindAsync(string token, CancellationToken ct)
    {
        var hash = TeamRules.TryHash(token);
        if (hash == null) return null;
        return await db.UserInvitations.AsNoTracking()
            .Where(i => i.Token == hash)
            .Select(i => new InvitationRow(i.Id, i.BusinessId, i.Email, i.Role, i.Status, i.ExpiresAt,
                i.Business.Name, i.Business.LogoUrl, i.Business.IsActive))
            .FirstOrDefaultAsync(ct);
    }

    private static InvitationLinkStatus StatusOf(InvitationStatus status, DateTime expiresAt) => status switch
    {
        InvitationStatus.Accepted => InvitationLinkStatus.Accepted,
        InvitationStatus.Cancelled => InvitationLinkStatus.Cancelled,
        InvitationStatus.Expired => InvitationLinkStatus.Expired,
        _ => expiresAt <= DateTime.UtcNow ? InvitationLinkStatus.Expired : InvitationLinkStatus.Valid,
    };

    /// <summary>El rol global es informativo: si falla no se bloquea la incorporación al equipo.</summary>
    private async Task TryAddMemberRoleAsync(ApplicationUser user)
    {
        try
        {
            var result = await userManager.AddToRoleAsync(user, AppRoles.Member);
            if (!result.Succeeded) logger.LogWarning("No se pudo asignar el rol global Member al usuario {UserId}", user.Id);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "El rol global Member no existe; se omite para el usuario {UserId}", user.Id);
        }
    }

    /// <summary>Devuelve la invitación a pendiente si la aceptación no se completó.</summary>
    private Task<int> ReleaseAsync(int invitationId)
        => db.UserInvitations
            .Where(i => i.Id == invitationId && i.Status == InvitationStatus.Accepted)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, InvitationStatus.Pending), CancellationToken.None);

    private ObjectResult ConflictProblem(string detail) => Problem(statusCode: StatusCodes.Status409Conflict, detail: detail);
}
