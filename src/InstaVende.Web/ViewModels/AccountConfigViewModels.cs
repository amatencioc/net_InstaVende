using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;

namespace InstaVende.Web.ViewModels;

// Tab 1 – Organización
public class OrganizacionViewModel
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Currency { get; set; }
}

// Tab 2 – Usuarios
public class UsuariosViewModel
{
    public List<BusinessUserItemViewModel> Users { get; set; } = new();
    public List<PendingInvitationViewModel> PendingInvitations { get; set; } = new();
    public int MaxUsers { get; set; } = 1;
    public string? InviteEmail { get; set; }
}

public class BusinessUserItemViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public string? AvatarInitial => Email?.Substring(0, 1).ToUpper();
}

public class PendingInvitationViewModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime ExpiresAt { get; set; }
}

// Tab 3 – Notificaciones
public class NotificacionesEmailViewModel
{
    public List<NotificationEmailItemViewModel> Emails { get; set; } = new();
    public string? NewEmail { get; set; }
}

public class NotificationEmailItemViewModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

// Wrapper para toda la página con los 4 tabs
public class AccountConfigViewModel
{
    public OrganizacionViewModel Organizacion { get; set; } = new();
    public UsuariosViewModel Usuarios { get; set; } = new();
    public NotificacionesEmailViewModel Notificaciones { get; set; } = new();
    public string? ActiveTab { get; set; } = "organizacion";
}
