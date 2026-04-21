using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;
namespace InstaVende.Web.ViewModels;

public class ChannelConfigViewModel
{
    public int Id { get; set; }
    public ChannelType ChannelType { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? PageId { get; set; }
    public string? InstagramAccountId { get; set; }
    [Required(ErrorMessage = "El token de acceso es obligatorio.")]
    public string AccessToken { get; set; } = string.Empty;
    public string? AppSecret { get; set; }
    [Required(ErrorMessage = "El token de verificación del webhook es obligatorio.")]
    public string WebhookVerifyToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class EmbeddedSignupCallbackViewModel
{
    /// <summary>Authorization code returned by Meta's Embedded Signup JS SDK.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>WhatsApp Business Account ID (from session_info extras).</summary>
    public string? WabaId { get; set; }
    /// <summary>Phone Number ID (from session_info extras, if available).</summary>
    public string? PhoneNumberId { get; set; }
}
