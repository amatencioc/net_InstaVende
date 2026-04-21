using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;

namespace InstaVende.Web.ViewModels;

public class ReminderTemplateViewModel
{
    public int Id { get; set; }
    public CustomerSegment Segment { get; set; }
    public int Order { get; set; }
    [Required, MaxLength(200)]
    public string Message { get; set; } = string.Empty;
    public string TimeWindow { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? MediaUrl { get; set; }
}

public class ReminderSegmentViewModel
{
    public CustomerSegment Segment { get; set; }
    public string SegmentLabel => Segment switch
    {
        CustomerSegment.Frio => "Frío",
        CustomerSegment.Tibio => "Tibio",
        CustomerSegment.Caliente => "Caliente",
        _ => Segment.ToString()
    };
    public string SegmentNote => Segment switch
    {
        CustomerSegment.Frio => "Para contactos fríos es mejor no insistir demasiado.",
        CustomerSegment.Tibio => "Un único recordatorio estratégico para contactos tibios.",
        CustomerSegment.Caliente => "Dos recordatorios para convertir el interés en compra.",
        _ => ""
    };
    public int MaxReminders => Segment switch { CustomerSegment.Frio => 1, _ => 2 };
    public List<ReminderTemplateViewModel> Reminders { get; set; } = new();
    public CustomerSegment ActiveSegment { get; set; } = CustomerSegment.Frio;
}
