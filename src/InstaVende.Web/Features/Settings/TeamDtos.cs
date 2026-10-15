using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;

namespace InstaVende.Web.Features.Settings;

// ── Equipo (panel) ───────────────────────────────────────────────────────────

public sealed record TeamMemberDto(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? PhoneNumber,
    UserRole Role,
    bool IsOwner,
    bool IsCurrentUser,
    DateTime JoinedAt);

public sealed record TeamInvitationDto(int Id, string Email, UserRole Role, DateTime CreatedAt, DateTime ExpiresAt);

/// <summary>Equipo del negocio.</summary>
/// <param name="Members">Propietario y miembros invitados.</param>
/// <param name="Invitations">Invitaciones pendientes no expiradas.</param>
/// <param name="MaxUsers">Usuarios permitidos por el plan (propietario incluido).</param>
/// <param name="UsedSeats">Miembros actuales (propietario incluido). Las invitaciones pendientes también reservan cupo.</param>
/// <param name="CanManage">true si el usuario actual puede invitar, cambiar roles o quitar miembros.</param>
public sealed record TeamDto(
    IReadOnlyList<TeamMemberDto> Members,
    IReadOnlyList<TeamInvitationDto> Invitations,
    int MaxUsers,
    int UsedSeats,
    bool CanManage);

public sealed record TeamInviteRequest(
    [property: Required(ErrorMessage = "Ingresa el email de la persona a invitar."), EmailAddress(ErrorMessage = "Ingresa un email válido."), StringLength(256)] string Email,
    [property: EnumDataType(typeof(UserRole), ErrorMessage = "Selecciona un rol válido.")] UserRole Role = UserRole.Member);

/// <summary>Invitación recién creada. <see cref="InviteUrl"/> solo se puede obtener ahora: el servidor guarda únicamente el hash del token.</summary>
public sealed record TeamInvitationCreatedDto(TeamInvitationDto Invitation, string InviteUrl);

public sealed record TeamRoleUpdateRequest(
    [property: EnumDataType(typeof(UserRole), ErrorMessage = "Selecciona un rol válido.")] UserRole Role);

// ── Invitación pública ───────────────────────────────────────────────────────

public enum InvitationLinkStatus
{
    Valid,
    Expired,
    Accepted,
    Cancelled,
}

/// <summary>Datos que ve quien abre el enlace. El email solo se expone mientras la invitación es válida.</summary>
/// <param name="HasAccount">true si ya existe una cuenta con ese email (debe iniciar sesión en lugar de registrarse).</param>
public sealed record InvitationDetailsDto(
    InvitationLinkStatus Status,
    string BusinessName,
    string? BusinessLogoUrl,
    string? Email,
    UserRole Role,
    DateTime ExpiresAt,
    bool HasAccount);

/// <summary>
/// Con sesión iniciada se ignora el cuerpo. Sin sesión se exigen nombre, apellido y contraseña
/// para crear la cuenta con el email invitado.
/// </summary>
public sealed record InvitationAcceptRequest(
    [property: StringLength(100, ErrorMessage = "El nombre admite hasta 100 caracteres.")] string? FirstName,
    [property: StringLength(100, ErrorMessage = "El apellido admite hasta 100 caracteres.")] string? LastName,
    [property: StringLength(100, ErrorMessage = "La contraseña admite hasta 100 caracteres.")] string? Password);
