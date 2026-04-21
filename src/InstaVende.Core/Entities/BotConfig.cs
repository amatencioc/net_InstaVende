using InstaVende.Core.Enums;
namespace InstaVende.Core.Entities;
public class BotConfig
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string BotName { get; set; } = "Asistente";
    public string? Personality { get; set; }
    public string? BaseSystemPrompt { get; set; }
    public InteractionLevel InteractionLevel { get; set; } = InteractionLevel.Standard;
    public string Language { get; set; } = "es";
    public string FallbackMessage { get; set; } = "Lo siento, no entendí tu mensaje. ¿Puedes reformularlo?";
    public bool EnableHandoff { get; set; } = true;
    public string? HandoffTriggerPhrase { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Negociación y descuentos
    public decimal DiscountLevel1 { get; set; } = 5;
    public decimal DiscountLevel2 { get; set; } = 10;
    public decimal DiscountLevel3 { get; set; } = 15;
    public decimal MaxDiscountPercent { get; set; } = 15;
    public decimal MinMarginPercent { get; set; } = 20;
    public decimal FreeShippingThreshold { get; set; } = 50;
    public decimal BundleDiscount { get; set; } = 5;
    public decimal LoyaltyDiscount { get; set; } = 3;

    // Políticas y configuración operativa
    public string? BusinessHours { get; set; }
    public string? ReturnPolicy { get; set; }
    public string? WarrantyPolicy { get; set; }
    public string? ShippingTimes { get; set; }
    public string? PaymentMethods { get; set; }
    public string? ActivePromotions { get; set; }

    public Business Business { get; set; } = null!;
    public ICollection<BotIntent> Intents { get; set; } = new List<BotIntent>();
    public ICollection<BotKnowledge> KnowledgeBase { get; set; } = new List<BotKnowledge>();
    public ICollection<ConversationFlow> Flows { get; set; } = new List<ConversationFlow>();
}
