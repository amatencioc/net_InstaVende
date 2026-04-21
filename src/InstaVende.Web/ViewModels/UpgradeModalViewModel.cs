namespace InstaVende.Web.ViewModels;

public class UpgradeModalViewModel
{
    public string Icon { get; set; } = "lock-fill";
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<(string Icon, string Title, string Desc)> Features { get; set; } = new();
    public string PlanRequired { get; set; } = "Pro";
    public string CtaUrl { get; set; } = "#";
}
