using System.ComponentModel.DataAnnotations;
namespace InstaVende.Web.ViewModels;

public class RegisterViewModel
{
    [Required][StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required][StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required][EmailAddress] public string Email { get; set; } = string.Empty;
    [Required][StringLength(100, MinimumLength = 6)][DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [DataType(DataType.Password)][Compare("Password")] public string ConfirmPassword { get; set; } = string.Empty;
    [Required][StringLength(200)] public string BusinessName { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required][EmailAddress] public string Email { get; set; } = string.Empty;
    [Required][DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Display(Name = "Recordarme")] public bool RememberMe { get; set; }
}
