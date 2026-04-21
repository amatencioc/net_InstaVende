using InstaVende.Core.Enums;

namespace InstaVende.Web.ViewModels;

public class MetricasViewModel
{
    public bool HasAccess { get; set; } = false;
    public int TotalConversations { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public double ConversionRate { get; set; }
}
