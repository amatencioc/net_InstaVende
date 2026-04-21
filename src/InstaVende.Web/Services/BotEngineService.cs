using System.Text.Json;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;

namespace InstaVende.Web.Services;

public class BotEngineService : IBotEngineService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly MasterPromptBuilder _promptBuilder;
    private readonly ILogger<BotEngineService> _logger;

    private const int MaxHistoryMessages = 10;
    private const int MaxToolIterations   = 5;

    public BotEngineService(
        AppDbContext db,
        IConfiguration config,
        MasterPromptBuilder promptBuilder,
        ILogger<BotEngineService> logger)
    {
        _db = db;
        _config = config;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    // ── Public entry point ────────────────────────────────────────────────────

    public async Task<string> ProcessMessageAsync(int businessId, int conversationId, string incomingMessage)
    {
        var botConfig = await _db.BotConfigs
            .Include(b => b.Intents.Where(i => i.IsActive))
            .Include(b => b.KnowledgeBase.Where(k => k.IsActive))
            .Include(b => b.Business)
            .FirstOrDefaultAsync(b => b.BusinessId == businessId && b.IsActive);

        if (botConfig == null) return "Hola, ¿en qué puedo ayudarte?";

        // Fast-path: handoff trigger phrase
        if (botConfig.EnableHandoff
            && !string.IsNullOrWhiteSpace(botConfig.HandoffTriggerPhrase)
            && incomingMessage.Contains(botConfig.HandoffTriggerPhrase, StringComparison.OrdinalIgnoreCase))
            return "Transferiendo con un agente humano. Por favor espera.";

        // Fast-path: static intent matching (no AI cost)
        foreach (var intent in botConfig.Intents.OrderByDescending(i => i.Priority))
        {
            try
            {
                var phrases = JsonSerializer.Deserialize<List<string>>(intent.TriggerPhrases) ?? new();
                if (phrases.Any(p => incomingMessage.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    return intent.Response;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deserializing TriggerPhrases for intent {Id}", intent.Id);
            }
        }

        // AI path: GPT-4o with master prompt + function calling
        var apiKey = _config["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                return await CallOpenAiWithToolsAsync(botConfig, businessId, conversationId, incomingMessage, apiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI error for business {Id}", businessId);
            }
        }

        // Final fallback: knowledge base keyword match
        var match = botConfig.KnowledgeBase
            .FirstOrDefault(k => incomingMessage.Contains(k.Question, StringComparison.OrdinalIgnoreCase));
        return match?.Answer ?? botConfig.FallbackMessage;
    }

    // ── OpenAI call with tool-calling loop ────────────────────────────────────

    private async Task<string> CallOpenAiWithToolsAsync(
        BotConfig cfg,
        int businessId,
        int conversationId,
        string userMsg,
        string apiKey)
    {
        // Load conversation + contact
        var conversation = await _db.Conversations
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        var contact = conversation?.Contact;

        var isReturning = contact != null &&
            await _db.Conversations.CountAsync(c => c.ContactId == contact.Id) > 1;

        // Load conversation history
        var history = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAt)
            .Take(MaxHistoryMessages)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        // Build placeholder values from real data
        var now = DateTime.Now;
        var placeholders = BuildPlaceholders(cfg, contact, conversation, isReturning, now);

        // Build system prompt from master templates
        var systemPrompt = _promptBuilder.BuildSystemPrompt(placeholders);
        var tools = _promptBuilder.LoadTools();

        // Assemble message list: system + history + current user message
        var messages = new List<ChatMessage> { ChatMessage.CreateSystemMessage(systemPrompt) };

        foreach (var msg in history)
        {
            if (msg.Direction == MessageDirection.Inbound)
                messages.Add(ChatMessage.CreateUserMessage(msg.Content));
            else
                messages.Add(ChatMessage.CreateAssistantMessage(msg.Content));
        }

        messages.Add(ChatMessage.CreateUserMessage(userMsg));

        // Configure LLM options
        var options = new ChatCompletionOptions
        {
            Temperature = 0.4f,
            MaxOutputTokenCount = 500,
        };
        foreach (var tool in tools) options.Tools.Add(tool);

        var client = new ChatClient("gpt-4o", apiKey);

        // Tool-calling loop: let the model invoke functions until it gives a final answer
        ChatCompletion completion;
        var iteration = 0;

        do
        {
            completion = await client.CompleteChatAsync(messages, options);

            if (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(new AssistantChatMessage(completion));

                foreach (var toolCall in completion.ToolCalls)
                {
                    var toolResult = await ExecuteToolCallAsync(toolCall, businessId, conversationId, cfg);
                    _logger.LogDebug("Tool {Name} executed for business {Id}", toolCall.FunctionName, businessId);
                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, toolResult));
                }
            }

            iteration++;
        }
        while (completion.FinishReason == ChatFinishReason.ToolCalls && iteration < MaxToolIterations);

        return completion.Content.FirstOrDefault()?.Text ?? cfg.FallbackMessage;
    }

    // ── Placeholder builder ───────────────────────────────────────────────────

    private static Dictionary<string, string> BuildPlaceholders(
        BotConfig cfg,
        Contact? contact,
        Conversation? conversation,
        bool isReturning,
        DateTime now)
    {
        return new Dictionary<string, string>
        {
            ["BOT_NAME"]               = cfg.BotName,
            ["COMPANY_NAME"]           = cfg.Business.Name,
            ["CURRENT_DATE"]           = now.ToString("yyyy-MM-dd"),
            ["CURRENT_HOUR"]           = now.Hour.ToString(),
            ["CURRENT_MINUTE"]         = now.Minute.ToString("D2"),
            ["DAY_OF_WEEK"]            = now.DayOfWeek.ToString(),
            ["IS_WEEKEND"]             = (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                                            .ToString().ToLower(),
            ["USER_NAME"]              = contact?.Name ?? "No disponible",
            ["USER_ID"]                = contact?.ExternalId ?? string.Empty,
            ["USER_COUNTRY"]           = string.Empty,
            ["CHANNEL"]                = conversation?.ChannelType.ToString() ?? string.Empty,
            ["IS_RETURNING_USER"]      = isReturning.ToString().ToLower(),
            ["USER_SEGMENT"]           = isReturning ? "recurrente" : "nuevo",
            ["TOTAL_PURCHASES"]        = "0",
            ["LAST_PURCHASE_SUMMARY"]  = "Sin compras previas",
            ["LAST_TOPIC"]             = "Ninguno",
            ["USER_PREFERENCES"]       = "Sin preferencias registradas",
            ["NEGOTIATION_ATTEMPT"]    = "0",
            ["PRODUCTS_DISCUSSED"]     = "Ninguno",
            ["DETECTED_SENTIMENT"]     = "neutral",
            ["DISCOUNT_LEVEL_1"]       = cfg.DiscountLevel1.ToString("F0"),
            ["DISCOUNT_LEVEL_2"]       = cfg.DiscountLevel2.ToString("F0"),
            ["DISCOUNT_LEVEL_3"]       = cfg.DiscountLevel3.ToString("F0"),
            ["MAX_DISCOUNT_PERCENT"]   = cfg.MaxDiscountPercent.ToString("F0"),
            ["MIN_MARGIN_PERCENT"]     = cfg.MinMarginPercent.ToString("F0"),
            ["FREE_SHIPPING_THRESHOLD"]= cfg.FreeShippingThreshold.ToString("F0"),
            ["BUNDLE_DISCOUNT"]        = cfg.BundleDiscount.ToString("F0"),
            ["LOYALTY_DISCOUNT"]       = cfg.LoyaltyDiscount.ToString("F0"),
            ["ACTIVE_PROMOTIONS"]      = cfg.ActivePromotions ?? "Sin promociones activas actualmente.",
            ["RETURN_POLICY"]          = cfg.ReturnPolicy     ?? "Consulta con nuestro equipo.",
            ["WARRANTY_POLICY"]        = cfg.WarrantyPolicy   ?? "Consulta con nuestro equipo.",
            ["SHIPPING_TIMES"]         = cfg.ShippingTimes    ?? "3-7 días hábiles.",
            ["PAYMENT_METHODS"]        = cfg.PaymentMethods   ?? "Tarjeta, transferencia y efectivo.",
            ["BUSINESS_HOURS"]         = cfg.BusinessHours    ?? "Lunes a Viernes 9:00-18:00.",
            ["AGENTS_AVAILABLE"]       = "1",
            ["ESTIMATED_WAIT_TIME"]    = "5",
            ["IS_WITHIN_BUSINESS_HOURS"] = "true",
            ["COMPANY_WEBSITE"]        = cfg.Business.WebsiteUrl    ?? string.Empty,
            ["COMPANY_PHONE"]          = cfg.Business.Phone         ?? string.Empty,
            ["COMPANY_EMAIL"]          = cfg.Business.Email         ?? string.Empty,
            ["BUSINESS_SECTOR"]        = cfg.Business.BusinessSector ?? string.Empty,
            ["SOCIAL_MEDIA"]           = cfg.Business.SocialMedia   ?? string.Empty,
        };
    }

    // ── Tool call dispatcher ──────────────────────────────────────────────────

    private async Task<string> ExecuteToolCallAsync(
        ChatToolCall toolCall,
        int businessId,
        int conversationId,
        BotConfig cfg)
    {
        try
        {
            using var doc  = JsonDocument.Parse(toolCall.FunctionArguments);
            var args = doc.RootElement;

            return toolCall.FunctionName switch
            {
                "buscar_productos"         => await BuscarProductosAsync(args, businessId),
                "obtener_producto"         => await ObtenerProductoAsync(args, businessId),
                "obtener_imagen_producto"  => await ObtenerImagenProductoAsync(args, businessId),
                "buscar_alternativas"      => await BuscarAlternativasAsync(args, businessId),
                "verificar_stock"          => await VerificarStockAsync(args, businessId),
                "calcular_descuento"       => CalcularDescuento(args, cfg),
                "obtener_promociones"      => ObtenerPromociones(cfg),
                "transferir_a_humano"      => await TransferirAHumanoAsync(args, conversationId),
                "obtener_historial_cliente"=> await ObtenerHistorialClienteAsync(args, businessId),
                "calcular_envio"           => Serialize(new { mensaje = "Cálculo de envío no disponible en este momento. Por favor consulta con un agente." }),
                "crear_pedido"             => Serialize(new { mensaje = "Para finalizar tu pedido un agente te contactará para confirmar los detalles." }),
                "registrar_interes"        => Serialize(new { mensaje = "Tu interés ha sido registrado. Te notificaremos cuando esté disponible." }),
                "rastrear_pedido"          => Serialize(new { mensaje = "Para rastrear tu pedido contacta a nuestro equipo de soporte." }),
                _                          => Serialize(new { error  = $"Función '{toolCall.FunctionName}' no reconocida." }),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {Tool}", toolCall.FunctionName);
            return Serialize(new { error = "Error al ejecutar la función. Por favor intenta de nuevo." });
        }
    }

    // ── Tool implementations ──────────────────────────────────────────────────

    private async Task<string> BuscarProductosAsync(JsonElement args, int businessId)
    {
        var query     = TryGet(args, "query");
        var categoria = TryGet(args, "categoria");
        var precioMin = args.TryGetProperty("precio_min", out var pm) ? (decimal?)pm.GetDecimal() : null;
        var precioMax = args.TryGetProperty("precio_max", out var px) ? (decimal?)px.GetDecimal() : null;
        var enStock   = args.TryGetProperty("solo_en_stock", out var s) && s.GetBoolean();
        var orden     = TryGet(args, "orden") ?? "relevancia";

        var q = _db.Products
            .Include(p => p.Category)
            .Where(p => p.BusinessId == businessId && p.IsActive);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Name.Contains(query) || (p.Description != null && p.Description.Contains(query)));

        if (!string.IsNullOrWhiteSpace(categoria))
            q = q.Where(p => p.Category != null && p.Category.Name.Contains(categoria));

        if (precioMin.HasValue) q = q.Where(p => p.Price >= precioMin.Value);
        if (precioMax.HasValue) q = q.Where(p => p.Price <= precioMax.Value);
        if (enStock)            q = q.Where(p => p.Stock > 0);

        q = orden switch
        {
            "precio_asc"  => q.OrderBy(p => p.Price),
            "precio_desc" => q.OrderByDescending(p => p.Price),
            "rating"      => q.OrderByDescending(p => p.IsFeatured),
            "mas_vendido" => q.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Name),
            _             => q.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Name),
        };

        var products = await q.Take(5)
            .Select(p => new
            {
                id          = p.Id.ToString(),
                nombre      = p.Name,
                precio      = p.Price,
                descripcion = p.Description,
                stock       = p.Stock,
                imagen      = p.ImageUrl,
                categoria   = p.Category != null ? p.Category.Name : (string?)null,
                destacado   = p.IsFeatured,
            })
            .ToListAsync();

        if (!products.Any())
            return Serialize(new { resultados = Array.Empty<object>(), mensaje = "No se encontraron productos con esos criterios." });

        return Serialize(new { resultados = products, total = products.Count });
    }

    private async Task<string> ObtenerProductoAsync(JsonElement args, int businessId)
    {
        if (!TryGetInt(args, "product_id", out var id))
            return Serialize(new { error = "product_id inválido." });

        var p = await _db.Products
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId && x.IsActive);

        if (p == null) return Serialize(new { error = "Producto no encontrado." });

        return Serialize(new
        {
            id          = p.Id.ToString(),
            nombre      = p.Name,
            precio      = p.Price,
            descripcion = p.Description,
            stock       = p.Stock,
            imagen      = p.ImageUrl,
            categoria   = p.Category?.Name,
            destacado   = p.IsFeatured,
        });
    }

    private async Task<string> ObtenerImagenProductoAsync(JsonElement args, int businessId)
    {
        if (!TryGetInt(args, "product_id", out var id))
            return Serialize(new { error = "product_id inválido." });

        var imageUrl = await _db.Products
            .Where(p => p.Id == id && p.BusinessId == businessId && p.IsActive)
            .Select(p => p.ImageUrl)
            .FirstOrDefaultAsync();

        return imageUrl != null
            ? Serialize(new { imagen = imageUrl })
            : Serialize(new { error = "Imagen no disponible." });
    }

    private async Task<string> BuscarAlternativasAsync(JsonElement args, int businessId)
    {
        if (!TryGetInt(args, "product_id", out var id))
            return Serialize(new { error = "product_id inválido." });

        var maxResultados = args.TryGetProperty("max_resultados", out var mr) ? mr.GetInt32() : 3;

        var original = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == businessId);

        if (original == null) return Serialize(new { error = "Producto no encontrado." });

        var alternatives = await _db.Products
            .Where(p => p.BusinessId == businessId
                     && p.IsActive
                     && p.Id != id
                     && p.CategoryId == original.CategoryId
                     && p.Stock > 0)
            .OrderByDescending(p => p.IsFeatured)
            .Take(maxResultados)
            .Select(p => new
            {
                id     = p.Id.ToString(),
                nombre = p.Name,
                precio = p.Price,
                stock  = p.Stock,
                imagen = p.ImageUrl,
            })
            .ToListAsync();

        return Serialize(new { alternativas = alternatives, total = alternatives.Count });
    }

    private async Task<string> VerificarStockAsync(JsonElement args, int businessId)
    {
        if (!TryGetInt(args, "product_id", out var id))
            return Serialize(new { error = "product_id inválido." });

        var p = await _db.Products
            .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId && x.IsActive);

        if (p == null) return Serialize(new { error = "Producto no encontrado." });

        return Serialize(new
        {
            product_id  = id.ToString(),
            nombre      = p.Name,
            stock       = p.Stock,
            disponible  = p.Stock > 0,
            pocas_unidades = p.Stock is > 0 and < 5,
        });
    }

    private static string CalcularDescuento(JsonElement args, BotConfig cfg)
    {
        var nivel = args.TryGetProperty("intento_negociacion", out var n) ? n.GetInt32() : 1;
        var esRecurrente = args.TryGetProperty("es_cliente_recurrente", out var r) && r.GetBoolean();

        var descuento = nivel switch
        {
            1 => cfg.DiscountLevel1,
            2 => cfg.DiscountLevel2,
            3 => cfg.DiscountLevel3,
            _ => cfg.MaxDiscountPercent,
        };

        if (esRecurrente) descuento = Math.Min(descuento + cfg.LoyaltyDiscount, cfg.MaxDiscountPercent);

        return Serialize(new
        {
            descuento_porcentaje = descuento,
            descuento_maximo     = cfg.MaxDiscountPercent,
            nivel_aplicado       = nivel,
        });
    }

    private static string ObtenerPromociones(BotConfig cfg)
    {
        var promos = cfg.ActivePromotions ?? "Sin promociones activas actualmente.";
        return Serialize(new { promociones = promos });
    }

    private async Task<string> TransferirAHumanoAsync(JsonElement args, int conversationId)
    {
        var motivo   = TryGet(args, "motivo")   ?? "solicitud_cliente";
        var prioridad = TryGet(args, "prioridad") ?? "media";
        var resumen  = TryGet(args, "resumen")  ?? string.Empty;

        var conv = await _db.Conversations.FindAsync(conversationId);
        if (conv != null)
        {
            conv.Status    = ConversationStatus.WaitingHuman;
            conv.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Handoff triggered for conversation {Id} — motivo: {Motivo}, prioridad: {Prioridad}. Resumen: {Resumen}",
            conversationId, motivo, prioridad, resumen);

        return Serialize(new
        {
            transferido = true,
            motivo,
            prioridad,
            mensaje = "El cliente ha sido transferido a un agente humano.",
        });
    }

    private async Task<string> ObtenerHistorialClienteAsync(JsonElement args, int businessId)
    {
        var userId = TryGet(args, "user_id");
        if (string.IsNullOrWhiteSpace(userId))
            return Serialize(new { error = "user_id requerido." });

        var contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.ExternalId == userId && c.BusinessId == businessId);

        if (contact == null)
            return Serialize(new { historial = Array.Empty<object>(), total_conversaciones = 0 });

        var totalConvs = await _db.Conversations.CountAsync(c => c.ContactId == contact.Id);
        var ultimaConv = await _db.Conversations
            .Where(c => c.ContactId == contact.Id)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => c.UpdatedAt)
            .FirstOrDefaultAsync();

        return Serialize(new
        {
            nombre               = contact.Name,
            primera_interaccion  = contact.FirstSeenAt,
            ultima_interaccion   = contact.LastSeenAt,
            total_conversaciones = totalConvs,
            ultima_conversacion  = ultimaConv,
        });
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string? TryGet(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static bool TryGetInt(JsonElement el, string key, out int value)
    {
        if (el.TryGetProperty(key, out var v))
        {
            if (v.ValueKind == JsonValueKind.Number) { value = v.GetInt32(); return true; }
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out value)) return true;
        }
        value = 0;
        return false;
    }

    private static string Serialize(object obj)
        => JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = false });
}

