using InstaVende.Core.Domain;

namespace InstaVende.Web.Services.Bot;

/// <summary>
/// Configuración del vendedor IA de un negocio, inmutable y cacheada
/// (ver <see cref="BotContextProvider"/>). No contiene entidades de EF.
/// </summary>
public sealed record BotContext
{
    public required int BusinessId { get; init; }
    public required string BusinessName { get; init; }
    public required bool IsActive { get; init; }
    public required string BotName { get; init; }
    public required string FallbackMessage { get; init; }

    public required bool EnableHandoff { get; init; }
    public string? HandoffTriggerPhrase { get; init; }
    public required string HandoffMessage { get; init; }
    public required bool AutoPauseOnHandoff { get; init; }

    public required string? Country { get; init; }
    public required string? Currency { get; init; }
    public string? BusinessHours { get; init; }

    public required NegotiationRules Negotiation { get; init; }
    public required string ActivePromotions { get; init; }
    public required IReadOnlyList<DeliveryZoneInfo> DeliveryZones { get; init; }
    public required IReadOnlyList<BotIntentRule> Intents { get; init; }
    public required IReadOnlyList<QuickAnswer> QuickAnswers { get; init; }
    public required string PurchaseConfirmationMessage { get; init; }

    /// <summary>Valores de los placeholders estables del system prompt (no dependen del cliente ni de la hora).</summary>
    public required IReadOnlyDictionary<string, string> StaticPlaceholders { get; init; }

    public string Money(decimal amount) => BusinessLocale.FormatMoney(amount, Currency);
    public TimeZoneInfo TimeZone => BusinessLocale.TimeZone(Country);
}

public sealed record NegotiationRules(
    decimal Level1, decimal Level2, decimal Level3,
    decimal MaxDiscountPercent, decimal LoyaltyDiscount, decimal BundleDiscount,
    decimal FreeShippingThreshold);

public sealed record DeliveryZoneInfo(string Name, decimal? Cost, string? Description);

/// <summary>Respuesta fija configurada por el comercio (legacy BotIntent) para frases concretas.</summary>
public sealed record BotIntentRule(string Name, IReadOnlyList<string> Phrases, string Response, int Priority);

/// <summary>Pregunta/respuesta legacy (BotKnowledge); se usa como atajo solo si no hay IA disponible.</summary>
public sealed record QuickAnswer(string Question, string Answer);
