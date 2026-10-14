using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;
namespace InstaVende.Web.ViewModels;

public class BotConfigViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del bot es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string BotName { get; set; } = "Asistente";
    public string? Personality { get; set; }
    public string? BaseSystemPrompt { get; set; }
    public InteractionLevel InteractionLevel { get; set; } = InteractionLevel.Standard;
    public string Language { get; set; } = "es";
    [Required(ErrorMessage = "El mensaje alternativo es obligatorio.")]
    public string FallbackMessage { get; set; } = "Lo siento, no entendí tu mensaje.";
    public bool EnableHandoff { get; set; } = true;
    public string? HandoffTriggerPhrase { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BotIntentViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del intent es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
    public string IntentName { get; set; } = string.Empty;
    [Required(ErrorMessage = "Las frases disparadoras son obligatorias.")]
    public string TriggerPhrases { get; set; } = string.Empty;
    [Required(ErrorMessage = "La respuesta es obligatoria.")]
    public string Response { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
}

public class BotKnowledgeViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "La pregunta es obligatoria.")]
    public string Question { get; set; } = string.Empty;
    [Required(ErrorMessage = "La respuesta es obligatoria.")]
    public string Answer { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BotPreviewMessageViewModel
{
    [Required(ErrorMessage = "El mensaje no puede estar vacío.")]
    public string Message { get; set; } = string.Empty;
}
