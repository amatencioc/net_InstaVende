using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;
namespace InstaVende.Web.ViewModels;

public class BotConfigViewModel
{
    public int Id { get; set; }
    [Required][StringLength(100)] public string BotName { get; set; } = "Asistente";
    public string? Personality { get; set; }
    public string? BaseSystemPrompt { get; set; }
    public InteractionLevel InteractionLevel { get; set; } = InteractionLevel.Standard;
    public string Language { get; set; } = "es";
    [Required] public string FallbackMessage { get; set; } = "Lo siento, no entendí tu mensaje.";
    public bool EnableHandoff { get; set; } = true;
    public string? HandoffTriggerPhrase { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BotIntentViewModel
{
    public int Id { get; set; }
    [Required][StringLength(200)] public string IntentName { get; set; } = string.Empty;
    [Required] public string TriggerPhrases { get; set; } = string.Empty;
    [Required] public string Response { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
}

public class BotKnowledgeViewModel
{
    public int Id { get; set; }
    [Required] public string Question { get; set; } = string.Empty;
    [Required] public string Answer { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BotPreviewMessageViewModel
{
    [Required] public string Message { get; set; } = string.Empty;
}
