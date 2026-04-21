using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;
namespace InstaVende.Web.ViewModels;

public class ReminderViewModel
{
    public int Id { get; set; }
    public int ContactId { get; set; }
    public string? ContactName { get; set; }
    public int? ConversationId { get; set; }
    [Required(ErrorMessage = "El mensaje es obligatorio.")]
    public string Message { get; set; } = string.Empty;
    public ChannelType ChannelType { get; set; }
    public ReminderStatus Status { get; set; }
    public string StatusLabel => Status switch
    {
        ReminderStatus.Pending   => "Pendiente",
        ReminderStatus.Sent      => "Enviado",
        ReminderStatus.Failed    => "Fallido",
        ReminderStatus.Cancelled => "Cancelado",
        _ => "Desconocido"
    };
    [Required(ErrorMessage = "La fecha y hora son obligatorias.")]
    public DateTime ScheduledAt { get; set; } = DateTime.Now.AddHours(24);
    public DateTime? SentAt { get; set; }
    public string? TemplateKey { get; set; }
    public DateTime CreatedAt { get; set; }
}
