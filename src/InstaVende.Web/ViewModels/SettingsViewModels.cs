using System.ComponentModel.DataAnnotations;
namespace InstaVende.Web.ViewModels;

public class AccountProfileViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresa un email válido.")]
    public string Email { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class BusinessProfileViewModel
{
    [Required(ErrorMessage = "El nombre del negocio es obligatorio.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Sector { get; set; }
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Ingresa un email válido.")]
    public string? Email { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? LogoUrl { get; set; }
}
