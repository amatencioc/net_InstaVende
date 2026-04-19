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
    [Required] public string AccessToken { get; set; } = string.Empty;
    public string? AppSecret { get; set; }
    [Required] public string WebhookVerifyToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
