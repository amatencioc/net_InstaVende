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

public class QrConnectionViewModel
{
    /// <summary>Phone number or WID reported by the local whatsapp-web.js client.</summary>
    public string? Phone { get; set; }
}
