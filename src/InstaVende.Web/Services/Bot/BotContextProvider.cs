using System.Globalization;
using System.Text;
using System.Text.Json;
using InstaVende.Core.Domain;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services.Bot;

/// <summary>
/// Construye y cachea el <see cref="BotContext"/> de cada negocio a partir de
/// BotConfig, VendorConfig, Business, KnowledgeEntry, PaymentMethod y DeliveryZone.
/// La caché se invalida automáticamente con <see cref="BotContextInvalidationInterceptor"/>.
/// </summary>
public class BotContextProvider
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-PE");
    private const int MaxKnowledgeEntries = 80;

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly BotCacheOptions _opts;
    private readonly ILogger<BotContextProvider> _logger;

    public BotContextProvider(AppDbContext db, IMemoryCache cache, IOptions<BotCacheOptions> opts, ILogger<BotContextProvider> logger)
    {
        _db = db;
        _cache = cache;
        _opts = opts.Value;
        _logger = logger;
    }

    public static string CacheKey(int businessId) => $"BotCtx_{businessId}";

    public static void Invalidate(IMemoryCache cache, int businessId) => cache.Remove(CacheKey(businessId));

    /// <summary>Devuelve el contexto del negocio o null si el negocio no existe.</summary>
    public async Task<BotContext?> GetAsync(int businessId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey(businessId), out BotContext? cached) && cached != null)
            return cached;

        var ctx = await BuildAsync(businessId, ct);
        if (ctx != null)
            _cache.Set(CacheKey(businessId), ctx, TimeSpan.FromMinutes(Math.Max(1, _opts.BotConfigTtlMinutes)));
        return ctx;
    }

    private async Task<BotContext?> BuildAsync(int businessId, CancellationToken ct)
    {
        var business = await _db.Businesses.AsNoTracking()
            .Include(b => b.VendorConfig)
            .FirstOrDefaultAsync(b => b.Id == businessId, ct);
        if (business == null) return null;

        // Sin fila BotConfig el bot está activo con valores por defecto (negocio recién creado).
        var cfg = await _db.BotConfigs.AsNoTracking()
            .Include(b => b.Intents.Where(i => i.IsActive))
            .Include(b => b.KnowledgeBase.Where(k => k.IsActive))
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.BusinessId == businessId, ct)
            ?? new BotConfig { BusinessId = businessId };

        var knowledge = await _db.KnowledgeEntries.AsNoTracking()
            .Where(k => k.BusinessId == businessId)
            .OrderByDescending(k => k.IsFavorite).ThenByDescending(k => k.UpdatedAt)
            .Take(MaxKnowledgeEntries)
            .Select(k => new { k.Category, k.Title, k.Content })
            .ToListAsync(ct);

        var payments = await _db.PaymentMethods.AsNoTracking()
            .Where(p => p.BusinessId == businessId && p.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync(ct);

        var zones = await _db.DeliveryZones.AsNoTracking()
            .Where(z => z.BusinessId == businessId && z.IsActive)
            .OrderBy(z => z.SortOrder).ThenBy(z => z.Name)
            .Select(z => new DeliveryZoneInfo(z.Name, z.Cost, z.Description))
            .ToListAsync(ct);

        var vendor   = business.VendorConfig;
        var currency = business.Currency;
        var country  = FirstNonBlank(vendor?.Country, business.Country) ?? BusinessLocale.DefaultCountry;
        var botName  = FirstNonBlank(vendor?.VendorName, cfg.BotName) ?? "Asistente";
        var handoffMessage = FirstNonBlank(vendor?.HandoffExampleMessage) ?? "Te comunico con un asesor, en breve te atiende. 🙌";
        var purchaseConfirmation = FirstNonBlank(vendor?.PurchaseConfirmationMessage)
                                   ?? "¡Gracias por tu compra! Estamos procesando tu pedido.";
        var promotions = FirstNonBlank(cfg.ActivePromotions) ?? "Sin promociones vigentes.";

        var negotiation = new NegotiationRules(
            cfg.DiscountLevel1, cfg.DiscountLevel2, cfg.DiscountLevel3,
            cfg.MaxDiscountPercent, cfg.LoyaltyDiscount, cfg.BundleDiscount, cfg.FreeShippingThreshold);

        var staticValues = new Dictionary<string, string>
        {
            ["BOT_NAME"]                      = botName,
            ["COMPANY_NAME"]                  = business.Name,
            ["BUSINESS_SECTOR"]               = FirstNonBlank(business.Sector) ?? "No especificado",
            ["BUSINESS_DESCRIPTION"]          = FirstNonBlank(vendor?.BusinessDescription, business.Description) ?? "No especificada",
            ["TARGET_AUDIENCE"]               = FirstNonBlank(vendor?.TargetAudience) ?? "Público general",
            ["COUNTRY"]                       = country,
            ["CURRENCY_SYMBOL"]               = BusinessLocale.CurrencySymbol(currency),
            ["COMPANY_PHONE"]                 = FirstNonBlank(business.Phone, business.WhatsAppNumber) ?? "No disponible",
            ["COMPANY_EMAIL"]                 = FirstNonBlank(business.Email) ?? "No disponible",
            ["COMPANY_WEBSITE"]               = FirstNonBlank(business.WebsiteUrl) ?? "No disponible",
            ["SOCIAL_MEDIA"]                  = FirstNonBlank(business.SocialMedia) ?? "No disponible",
            ["VENDOR_PERSONALITY"]            = BuildPersonality(vendor),
            ["VENDOR_RULES"]                  = FirstNonBlank(vendor?.Rules) ?? "Sin reglas adicionales.",
            ["HANDOFF_SITUATIONS"]            = BuildHandoffSituations(vendor?.HumanHandoffSituations),
            ["HANDOFF_MESSAGE"]               = handoffMessage,
            ["PURCHASE_CONFIRMATION_MESSAGE"] = purchaseConfirmation,
            ["DISCOUNT_LEVEL_1"]              = Num(cfg.DiscountLevel1),
            ["DISCOUNT_LEVEL_2"]              = Num(cfg.DiscountLevel2),
            ["DISCOUNT_LEVEL_3"]              = Num(cfg.DiscountLevel3),
            ["MAX_DISCOUNT_PERCENT"]          = Num(cfg.MaxDiscountPercent),
            ["LOYALTY_DISCOUNT"]              = Num(cfg.LoyaltyDiscount),
            ["BUNDLE_DISCOUNT"]               = Num(cfg.BundleDiscount),
            ["FREE_SHIPPING_THRESHOLD"]       = Num(cfg.FreeShippingThreshold),
            ["BUSINESS_HOURS"]                = FirstNonBlank(cfg.BusinessHours) ?? "No especificado",
            ["RETURN_POLICY"]                 = FirstNonBlank(cfg.ReturnPolicy) ?? "No especificada; deriva la consulta a un asesor.",
            ["WARRANTY_POLICY"]               = FirstNonBlank(cfg.WarrantyPolicy) ?? "No especificada; deriva la consulta a un asesor.",
            ["SHIPPING_TIMES"]                = FirstNonBlank(cfg.ShippingTimes) ?? "No especificados; deriva la consulta a un asesor.",
            ["SHIPPING_ZONES"]                = BuildZones(zones, currency),
            ["PAYMENT_METHODS"]               = BuildPayments(payments, cfg.PaymentMethods),
            ["ACTIVE_PROMOTIONS"]             = promotions,
            ["KNOWLEDGE_BASE"]                = BuildKnowledge(knowledge.Select(k => (k.Category, k.Title, k.Content)), cfg.KnowledgeBase),
        };

        return new BotContext
        {
            BusinessId                  = businessId,
            BusinessName                = business.Name,
            IsActive                    = cfg.IsActive,
            BotName                     = botName,
            FallbackMessage             = FirstNonBlank(cfg.FallbackMessage) ?? "Lo siento, no entendí tu mensaje. ¿Puedes reformularlo?",
            EnableHandoff               = cfg.EnableHandoff,
            HandoffTriggerPhrase        = FirstNonBlank(cfg.HandoffTriggerPhrase),
            HandoffMessage              = handoffMessage,
            AutoPauseOnHandoff          = vendor?.AutoPauseOnHandoff ?? true,
            Country                     = country,
            Currency                    = currency,
            BusinessHours               = cfg.BusinessHours,
            Negotiation                 = negotiation,
            ActivePromotions            = promotions,
            DeliveryZones               = zones,
            Intents                     = ParseIntents(cfg.Intents),
            QuickAnswers                = cfg.KnowledgeBase
                                            .Where(k => k.Question.Trim().Length >= 4 && !string.IsNullOrWhiteSpace(k.Answer))
                                            .Select(k => new QuickAnswer(k.Question.Trim(), k.Answer)).ToList(),
            PurchaseConfirmationMessage = purchaseConfirmation,
            StaticPlaceholders          = staticValues,
        };
    }

    // ── Builders de bloques de texto ─────────────────────────────────────────

    private static readonly string[] ResponseLengthLabels =
        ["muy conciso (1 oración)", "conciso (1-2 oraciones)", "equilibrado (2-3 oraciones)", "detallado (3-4 oraciones)", "muy detallado (hasta 5 oraciones)"];

    private static string BuildPersonality(VendorConfig? v)
    {
        if (v == null) return "- Tono amable, cercano y profesional.\n- Respuestas equilibradas (2-3 oraciones).\n- Puedes usar 1-2 emojis por mensaje.";

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(v.VendorGender)) sb.AppendLine($"- Género del vendedor: {v.VendorGender}.");
        if (!string.IsNullOrWhiteSpace(v.CommunicationStyle)) sb.AppendLine($"- Estilo de comunicación: {v.CommunicationStyle.Trim()}");
        if (!string.IsNullOrWhiteSpace(v.SalesStyle)) sb.AppendLine($"- Estilo de ventas: {v.SalesStyle.Trim()}");
        sb.AppendLine($"- Longitud de respuesta: {ResponseLengthLabels[Math.Clamp(v.ResponseLength, 1, 5) - 1]}.");
        if (v.UseEmojis)
        {
            var palette = ParseEmojiPalette(v.EmojiPalette);
            sb.AppendLine(palette.Length > 0
                ? $"- Usa emojis con moderación y solo de esta paleta: {string.Join(" ", palette)}"
                : "- Usa emojis con moderación (1-2 por mensaje).");
        }
        else sb.AppendLine("- No uses emojis.");
        sb.AppendLine(v.UseOpeningPunctuation
            ? "- Usa signos de apertura (¿ ¡)."
            : "- Omite los signos de apertura (¿ ¡), como en un chat informal.");
        if (!string.IsNullOrWhiteSpace(v.WordsToAvoid)) sb.AppendLine($"- Nunca uses estas palabras o frases: {v.WordsToAvoid.Trim()}");
        if (!string.IsNullOrWhiteSpace(v.WelcomeMessage)) sb.AppendLine($"- Mensaje de bienvenida preferido para el primer contacto: \"{v.WelcomeMessage.Trim()}\"");
        return sb.ToString().TrimEnd();
    }

    private static string[] ParseEmojiPalette(string? palette)
    {
        if (string.IsNullOrWhiteSpace(palette)) return [];
        try
        {
            if (palette.TrimStart().StartsWith('['))
                return JsonSerializer.Deserialize<string[]>(palette)?.Where(e => !string.IsNullOrWhiteSpace(e)).ToArray() ?? [];
        }
        catch (JsonException) { /* formato libre */ }
        return palette.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string BuildHandoffSituations(string? situations)
    {
        var items = SplitList(situations);
        if (items.Count == 0)
            items = ["El cliente pide hablar con una persona.", "Reclamos o problemas con un pedido ya entregado.", "Consultas que no puedes resolver con la información disponible."];
        return string.Join("\n", items.Select(i => $"  - {i}"));
    }

    private static string BuildZones(IReadOnlyList<DeliveryZoneInfo> zones, string? currency)
    {
        if (zones.Count == 0) return "No hay zonas de entrega configuradas; si preguntan por envíos, deriva a un asesor.";
        return string.Join("\n", zones.Select(z =>
        {
            var cost = z.Cost is null ? "costo a coordinar" : z.Cost == 0 ? "envío gratis" : BusinessLocale.FormatMoney(z.Cost.Value, currency);
            return string.IsNullOrWhiteSpace(z.Description) ? $"- {z.Name}: {cost}" : $"- {z.Name}: {cost}. {z.Description.Trim()}";
        }));
    }

    private static string BuildPayments(IReadOnlyList<PaymentMethod> methods, string? legacyText)
    {
        if (methods.Count == 0)
            return FirstNonBlank(legacyText) ?? "No hay métodos de pago configurados; si preguntan cómo pagar, deriva a un asesor.";

        var sb = new StringBuilder();
        foreach (var m in methods)
        {
            sb.Append($"- {m.Name} ({PaymentTypeLabel(m.Type)})");
            if (!string.IsNullOrWhiteSpace(m.Instructions)) sb.Append($": {m.Instructions.Trim().ReplaceLineEndings(" / ")}");
            if (!string.IsNullOrWhiteSpace(m.AccountNumber)) sb.Append($" | Cuenta: {m.AccountNumber}");
            if (!string.IsNullOrWhiteSpace(m.AccountAlias)) sb.Append($" | Alias: {m.AccountAlias}");
            if (!string.IsNullOrWhiteSpace(m.PaymentLink)) sb.Append($" | Link: {m.PaymentLink}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    public static string PaymentTypeLabel(PaymentMethodType type) => type switch
    {
        PaymentMethodType.BankTransfer => "transferencia bancaria",
        PaymentMethodType.CreditCard   => "tarjeta",
        PaymentMethodType.Cash         => "efectivo / contraentrega",
        PaymentMethodType.MercadoPago  => "Mercado Pago",
        PaymentMethodType.PayPal       => "PayPal",
        PaymentMethodType.Stripe       => "Stripe",
        _                              => "otro",
    };

    private static string BuildKnowledge(IEnumerable<(KnowledgeCategory Category, string Title, string Content)> entries,
        IEnumerable<BotKnowledge> legacy)
    {
        var sb = new StringBuilder();
        foreach (var grp in entries.GroupBy(e => e.Category))
        {
            sb.AppendLine($"[{grp.Key}]");
            foreach (var (_, title, content) in grp)
                sb.AppendLine($"- {title.Trim()}: {content.Trim().ReplaceLineEndings(" ")}");
        }
        var qa = legacy.Where(k => !string.IsNullOrWhiteSpace(k.Question) && !string.IsNullOrWhiteSpace(k.Answer)).ToList();
        if (qa.Count > 0)
        {
            sb.AppendLine("[Preguntas frecuentes]");
            foreach (var k in qa) sb.AppendLine($"- {k.Question.Trim()}: {k.Answer.Trim().ReplaceLineEndings(" ")}");
        }
        return sb.Length == 0 ? "Sin información adicional." : sb.ToString().TrimEnd();
    }

    private IReadOnlyList<BotIntentRule> ParseIntents(IEnumerable<BotIntent> intents)
    {
        var list = new List<BotIntentRule>();
        foreach (var intent in intents.OrderByDescending(i => i.Priority))
        {
            List<string> phrases;
            try
            {
                phrases = intent.TriggerPhrases.TrimStart().StartsWith('[')
                    ? JsonSerializer.Deserialize<List<string>>(intent.TriggerPhrases) ?? []
                    : SplitList(intent.TriggerPhrases);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "TriggerPhrases inválido en el intent {Id}; se ignora.", intent.Id);
                continue;
            }
            var valid = phrases.Select(p => p.Trim()).Where(p => p.Length >= 3).ToList();
            if (valid.Count > 0 && !string.IsNullOrWhiteSpace(intent.Response))
                list.Add(new BotIntentRule(intent.IntentName, valid, intent.Response, intent.Priority));
        }
        return list;
    }

    private static List<string> SplitList(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(['\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(s => s.TrimStart('-', '•', '*', ' ')).Where(s => s.Length > 0).ToList();

    private static string Num(decimal value) => value.ToString("0.##", Es);

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
