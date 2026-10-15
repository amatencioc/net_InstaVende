using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Features.Settings;

/// <summary>
/// Equipo del negocio: propietario, miembros invitados e invitaciones pendientes.
/// Todos los miembros pueden verlo; solo los administradores pueden modificarlo.
/// </summary>
[Route("api/team")]
public sealed class TeamController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IOptions<AppOptions> appOptions,
    IConfiguration configuration,
    ILogger<TeamController> logger) : TenantController
{
    private int MaxUsers => TeamRules.MaxUsers(configuration);

    [HttpGet]
    [ProducesResponseType<TeamDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamDto>> Get(CancellationToken ct)
    {
        var currentUserId = userManager.GetUserId(User);

        var owner = await db.Businesses.AsNoTracking()
            .Where(b => b.Id == BusinessId)
            .Select(b => new TeamMemberDto(b.User.Id, b.User.Email ?? string.Empty, b.User.FirstName, b.User.LastName,
                b.User.AvatarUrl, b.User.PhoneNumber, UserRole.Admin, true, b.UserId == currentUserId, b.CreatedAt))
            .FirstOrDefaultAsync(ct);
        if (owner == null) return NotFoundProblem();

        var members = await db.BusinessUsers.AsNoTracking()
            .Where(bu => bu.BusinessId == BusinessId)
            .OrderBy(bu => bu.JoinedAt)
            .Select(bu => new TeamMemberDto(bu.UserId, bu.User.Email ?? string.Empty, bu.User.FirstName, bu.User.LastName,
                bu.User.AvatarUrl, bu.User.PhoneNumber, bu.Role, false, bu.UserId == currentUserId, bu.JoinedAt))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var invitations = await db.UserInvitations.AsNoTracking()
            .Where(i => i.BusinessId == BusinessId && i.Status == InvitationStatus.Pending && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new TeamInvitationDto(i.Id, i.Email, i.Role, i.CreatedAt, i.ExpiresAt))
            .ToListAsync(ct);

        return new TeamDto([owner, .. members], invitations, MaxUsers, 1 + members.Count, Tenant.IsAdmin);
    }

    /// <summary>
    /// Crea una invitación y devuelve su enlace. No hay envío de emails: el administrador comparte el enlace.
    /// Las invitaciones vigentes reservan cupo dentro del límite del plan.
    /// </summary>
    [HttpPost("invitations"), RequireBusinessAdmin]
    [ProducesResponseType<TeamInvitationCreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeamInvitationCreatedDto>> Invite(TeamInviteRequest request, CancellationToken ct)
    {
        var email = TeamRules.NormalizeEmail(request.Email);
        var normalized = userManager.NormalizeEmail(email);

        // Solo se comprueba la pertenencia a ESTE negocio: no se revela si el email tiene cuenta en otro.
        var isMember = await db.Businesses.AnyAsync(b => b.Id == BusinessId && b.User.NormalizedEmail == normalized, ct)
            || await db.BusinessUsers.AnyAsync(bu => bu.BusinessId == BusinessId && bu.User.NormalizedEmail == normalized, ct);
        if (isMember) return ConflictProblem("Esa persona ya forma parte de tu equipo.");

        var now = DateTime.UtcNow;
        var pending = await db.UserInvitations
            .Where(i => i.BusinessId == BusinessId && i.Status == InvitationStatus.Pending)
            .ToListAsync(ct);
        foreach (var expired in pending.Where(i => i.ExpiresAt <= now)) expired.Status = InvitationStatus.Expired;
        var active = pending.Where(i => i.ExpiresAt > now).ToList();

        if (active.Any(i => TeamRules.NormalizeEmail(i.Email) == email))
            return ConflictProblem("Ya hay una invitación vigente para ese email. Genera un nuevo enlace desde la lista de pendientes o cancélala.");

        var members = 1 + await db.BusinessUsers.CountAsync(bu => bu.BusinessId == BusinessId, ct);
        if (members + active.Count >= MaxUsers)
            return ConflictProblem($"Alcanzaste el límite de {MaxUsers} usuarios de tu plan (las invitaciones pendientes también ocupan cupo).");

        var (token, hash) = TeamRules.NewToken();
        var invitation = new UserInvitation
        {
            BusinessId = BusinessId,
            Email = email,
            Role = request.Role,
            Token = hash,
            Status = InvitationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.Add(TeamRules.InvitationLifetime),
        };
        db.UserInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Negocio {BusinessId}: invitación {InvitationId} creada con rol {Role}", BusinessId, invitation.Id, invitation.Role);
        return StatusCode(StatusCodes.Status201Created, WithLink(invitation, token));
    }

    /// <summary>
    /// Genera un enlace nuevo para una invitación pendiente (el anterior deja de funcionar) y renueva su vigencia.
    /// Necesario porque el enlace original no se puede recuperar: solo se guarda el hash del token.
    /// </summary>
    [HttpPost("invitations/{id:int}/regenerate"), RequireBusinessAdmin]
    [ProducesResponseType<TeamInvitationCreatedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeamInvitationCreatedDto>> Regenerate(int id, CancellationToken ct)
    {
        var invitation = await db.UserInvitations
            .FirstOrDefaultAsync(i => i.Id == id && i.BusinessId == BusinessId && i.Status == InvitationStatus.Pending, ct);
        if (invitation == null) return NotFoundProblem("La invitación no existe o ya no está pendiente.");

        var now = DateTime.UtcNow;
        if (invitation.ExpiresAt <= now)
        {
            // Una invitación expirada no reservaba cupo: al renovarla debe haber sitio
            var members = 1 + await db.BusinessUsers.CountAsync(bu => bu.BusinessId == BusinessId, ct);
            var active = await db.UserInvitations.CountAsync(i => i.BusinessId == BusinessId
                && i.Status == InvitationStatus.Pending && i.ExpiresAt > now, ct);
            if (members + active >= MaxUsers)
                return ConflictProblem($"Alcanzaste el límite de {MaxUsers} usuarios de tu plan.");
        }

        var (token, hash) = TeamRules.NewToken();
        invitation.Token = hash;
        invitation.ExpiresAt = now.Add(TeamRules.InvitationLifetime);
        await db.SaveChangesAsync(ct);
        return WithLink(invitation, token);
    }

    [HttpDelete("invitations/{id:int}"), RequireBusinessAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelInvitation(int id, CancellationToken ct)
    {
        var updated = await db.UserInvitations
            .Where(i => i.Id == id && i.BusinessId == BusinessId && i.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, InvitationStatus.Cancelled), ct);
        return updated == 0 ? NotFoundProblem("La invitación no existe o ya no está pendiente.") : NoContent();
    }

    [HttpPut("members/{userId}/role"), RequireBusinessAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRole(string userId, TeamRoleUpdateRequest request, CancellationToken ct)
    {
        if (await IsOwnerAsync(userId, ct)) return ConflictProblem("El propietario siempre es administrador; su rol no se puede cambiar.");

        var membership = await db.BusinessUsers.FirstOrDefaultAsync(bu => bu.BusinessId == BusinessId && bu.UserId == userId, ct);
        if (membership == null) return NotFoundProblem("El usuario no forma parte de tu equipo.");
        if (membership.Role == request.Role) return NoContent();

        if (membership.Role == UserRole.Admin && await IsLastAdminAsync(ct))
            return ConflictProblem("Debe quedar al menos un administrador en el equipo.");

        membership.Role = request.Role;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Negocio {BusinessId}: rol de {UserId} cambiado a {Role}", BusinessId, userId, request.Role);
        return NoContent();
    }

    [HttpDelete("members/{userId}"), RequireBusinessAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(string userId, CancellationToken ct)
    {
        if (await IsOwnerAsync(userId, ct)) return ConflictProblem("No se puede quitar al propietario del negocio.");

        var membership = await db.BusinessUsers.FirstOrDefaultAsync(bu => bu.BusinessId == BusinessId && bu.UserId == userId, ct);
        if (membership == null) return NotFoundProblem("El usuario no forma parte de tu equipo.");

        if (membership.Role == UserRole.Admin && userId == userManager.GetUserId(User) && await IsLastAdminAsync(ct))
            return ConflictProblem("Eres el único administrador: asigna el rol a otra persona antes de salir del equipo.");

        db.BusinessUsers.Remove(membership);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Negocio {BusinessId}: usuario {UserId} quitado del equipo", BusinessId, userId);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Task<bool> IsOwnerAsync(string userId, CancellationToken ct)
        => db.Businesses.AnyAsync(b => b.Id == BusinessId && b.UserId == userId, ct);

    /// <summary>El propietario cuenta siempre como administrador.</summary>
    private async Task<bool> IsLastAdminAsync(CancellationToken ct)
        => 1 + await db.BusinessUsers.CountAsync(bu => bu.BusinessId == BusinessId && bu.Role == UserRole.Admin, ct) <= 1;

    private TeamInvitationCreatedDto WithLink(UserInvitation invitation, string token) => new(
        new TeamInvitationDto(invitation.Id, invitation.Email, invitation.Role, invitation.CreatedAt, invitation.ExpiresAt),
        TeamRules.InviteUrl(Request, appOptions.Value, token));
}
