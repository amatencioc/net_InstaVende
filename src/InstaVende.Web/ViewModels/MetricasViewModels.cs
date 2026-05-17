using InstaVende.Core.Enums;

namespace InstaVende.Web.ViewModels;

public class MetricasViewModel
{
    public bool HasAccess { get; set; } = true;
    public int TotalConversations { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public double ConversionRate { get; set; }
}
