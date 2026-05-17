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
            .AsNoTracking()
            .Include(b => b.Intents.Where(i => i.IsActive))
            .Include(b => b.KnowledgeBase.Where(k => k.IsActive))
            .Include(b => b.Business)
                .ThenInclude(biz => biz.VendorConfig)
            .FirstOrDefaultAsync(b => b.BusinessId == businessId && b.IsActive);

        if (botConfig == null) return "Hola, ¿en qué puedo ayudarte?";

        var msg = incomingMessage.Trim();

        // Fast-path: handoff trigger phrase
        if (botConfig.EnableHandoff
            && !string.IsNullOrWhiteSpace(botConfig.HandoffTriggerPhrase)
            && msg.Contains(botConfig.HandoffTriggerPhrase, StringComparison.OrdinalIgnoreCase))
            return "Transferiendo con un agente humano. Por favor espera.";

        // ── Fast-path commands (no AI cost, always work) ────────────────────
        var command = DetectCommand(msg);
        if (command != null)
        {
            return command switch
            {
                "catalog"     => await GenerateCatalogAsync(businessId, botConfig),
                "help"        => GenerateHelpText(botConfig),
                "cart"        => await GenerateCartAsync(conversationId, botConfig),
                "order"       => await ConfirmOrderAsync(businessId, conversationId, botConfig),
                "clear_cart"  => await ClearCartAsync(conversationId, botConfig),
                "status"      => await GetOrderStatusAsync(conversationId, botConfig),
                "add_to_cart" => await AddToCartFromMessageAsync(businessId, conversationId, msg, botConfig),
                _             => null!
            };
        }

        // Fast-path: static intent matching (no AI cost)
        foreach (var intent in botConfig.Intents.OrderByDescending(i => i.Priority))
        {
            try
            {
                var phrases = JsonSerializer.Deserialize<List<string>>(intent.TriggerPhrases) ?? new();
                if (phrases.Any(p => msg.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    return intent.Response;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deserializing TriggerPhrases for intent {Id}", intent.Id);
            }
        }

        // Fast-path: knowledge base keyword match
        var kbMatch = botConfig.KnowledgeBase
            .FirstOrDefault(k => msg.Contains(k.Question, StringComparison.OrdinalIgnoreCase));
        if (kbMatch != null) return kbMatch.Answer;

        // AI path: GPT-4o with master prompt + function calling
        var apiKey = _config["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                // Load KnowledgeEntries (configured in Mi Vendedor → Base de Conocimiento)
                var knowledgeEntries = await _db.KnowledgeEntries
                    .AsNoTracking()
                    .Where(k => k.BusinessId == businessId)
                    .OrderByDescending(k => k.IsFavorite)
                    .Take(30)
                    .ToListAsync();

                return await CallOpenAiWithToolsAsync(
                    botConfig, businessId, conversationId, msg, apiKey, knowledgeEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI error for business {Id}", businessId);
            }
        }

        return botConfig.FallbackMessage;
    }

    // ── Command detection (mirrors the Python MVP bot) ───────────────────────

    private static readonly Dictionary<string, string[]> _commands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["catalog"]    = ["#catalogo", "#catálogo", "ver productos", "mostrar productos", "lista de productos",
                          "que productos tienen", "qué productos tienen", "que tienen", "qué tienen",
                          "que vendes", "qué vendes", "que venden", "qué venden", "que hay", "qué hay",
                          "ver catalogo", "ver catálogo", "mostrar catalogo"],
        ["cart"]       = ["#carrito", "mi carrito", "ver carrito", "ver mi carrito"],
        ["order"]      = ["#pedido", "hacer pedido", "confirmar pedido", "quiero pedir", "confirmar compra"],
        ["clear_cart"] = ["#limpiar", "limpiar carrito", "vaciar carrito", "borrar carrito"],
        ["help"]       = ["#ayuda", "ayuda", "help", "comandos", "como funciona"],
        ["status"]     = ["#estado", "mi pedido", "estado de mi pedido", "donde esta mi pedido", "donde está mi pedido"],
    };

    private static string? DetectCommand(string msg)
    {
        var lower = msg.ToLowerInvariant();
        foreach (var (cmd, triggers) in _commands)
            if (triggers.Any(t => lower == t || lower.StartsWith(t) || lower.Contains(t)))
                return cmd;

        // "agregar 1", "quiero el 3", "dame el 2"
        if (System.Text.RegularExpressions.Regex.IsMatch(lower, @"^(agregar|añadir|quiero el|dame el|producto)\s+\d+"))
            return "add_to_cart";

        return null;
    }

    // ── Command handlers ──────────────────────────────────────────────────────

    private async Task<string> GenerateCatalogAsync(int businessId, BotConfig cfg)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.BusinessId == businessId && p.IsActive && p.Stock > 0)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Take(20)
            .ToListAsync();

        if (!products.Any())
            return $"😔 No tenemos productos disponibles en este momento. ¡Vuelve pronto!\n\nEscribe *#ayuda* para los comandos disponibles.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📦 *CATÁLOGO DE {cfg.Business.Name.ToUpperInvariant()}*");
        sb.AppendLine(new string('─', 26));

        foreach (var (p, i) in products.Select((p, i) => (p, i + 1)))
        {
            sb.AppendLine($"\n*{i}. {p.Name}*");
            sb.AppendLine($"💰 S/ {p.Price:N2}");
            if (!string.IsNullOrEmpty(p.Description))
                sb.AppendLine($"📝 {p.Description}");
            sb.AppendLine(p.Stock > 5 ? "✅ Disponible" : $"⚠️ Solo {p.Stock} en stock");
        }

        sb.AppendLine("\n" + new string('─', 26));
        sb.AppendLine("💬 Escribe el número del producto para agregarlo al carrito");
        sb.AppendLine("Ej: *agregar 1*");
        sb.AppendLine("\n🛒 *#carrito* → Ver carrito  |  📦 *#pedido* → Confirmar");
        return sb.ToString();
    }

    private static string GenerateHelpText(BotConfig cfg) =>
        $"🤖 *COMANDOS DISPONIBLES*\n" +
        "─────────────────────────\n" +
        "📋 *#catalogo* → Ver todos los productos\n" +
        "🛒 *#carrito* → Ver tu carrito\n" +
        "📦 *#pedido* → Confirmar tu pedido\n" +
        "🗑️ *#limpiar* → Vaciar carrito\n" +
        "📊 *#estado* → Estado de tu último pedido\n" +
        "❓ *#ayuda* → Ver esta ayuda\n\n" +
        "💬 También puedes escribirme naturalmente y te ayudo 😊";

    private async Task<string> GenerateCartAsync(int conversationId, BotConfig cfg)
    {
        var order = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.ConversationId == conversationId
                                   && o.Status == InstaVende.Core.Enums.OrderStatus.Pending);

        if (order == null || !order.Items.Any())
            return "🛒 Tu carrito está vacío.\n\nEscribe *#catalogo* para ver nuestros productos. 😊";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("🛒 *TU CARRITO*");
        sb.AppendLine(new string('─', 26));

        decimal total = 0;
        foreach (var item in order.Items)
        {
            var subtotal = item.UnitPrice * item.Quantity;
            total += subtotal;
            sb.AppendLine($"\n• {item.Product.Name}");
            sb.AppendLine($"  {item.Quantity} × S/ {item.UnitPrice:N2} = S/ {subtotal:N2}");
        }

        sb.AppendLine("\n" + new string('─', 26));
        sb.AppendLine($"💰 *TOTAL: S/ {total:N2}*");
        sb.AppendLine("\n📦 Escribe *#pedido* para confirmar tu compra");
        sb.AppendLine("🗑️ Escribe *#limpiar* para vaciar el carrito");
        return sb.ToString();
    }

    private async Task<string> ConfirmOrderAsync(int businessId, int conversationId, BotConfig cfg)
    {
        var conv = await _db.Conversations.Include(c => c.Contact).FirstOrDefaultAsync(c => c.Id == conversationId);
        var pendingOrder = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.ConversationId == conversationId
                                   && o.BusinessId == businessId
                                   && o.Status == InstaVende.Core.Enums.OrderStatus.Pending);

        if (pendingOrder == null || !pendingOrder.Items.Any())
            return "📦 No tienes productos en el carrito.\n\nEscribe *#catalogo* para ver nuestros productos. 😊";

        pendingOrder.Status = InstaVende.Core.Enums.OrderStatus.Confirmed;
        pendingOrder.Total  = pendingOrder.Items.Sum(i => i.UnitPrice * i.Quantity);
        await _db.SaveChangesAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("✅ *¡PEDIDO CONFIRMADO!*");
        sb.AppendLine(new string('─', 26));
        sb.AppendLine($"📋 Pedido #{pendingOrder.OrderNumber}");
        foreach (var item in pendingOrder.Items)
            sb.AppendLine($"• {item.Product.Name} × {item.Quantity} = S/ {item.UnitPrice * item.Quantity:N2}");
        sb.AppendLine(new string('─', 26));
        sb.AppendLine($"💰 *Total: S/ {pendingOrder.Total:N2}*");
        sb.AppendLine("\n🚀 En breve te contactaremos para coordinar el pago y entrega.");
        sb.AppendLine("¡Gracias por tu compra! 🙌");
        return sb.ToString();
    }

    private async Task<string> ClearCartAsync(int conversationId, BotConfig cfg)
    {
        var pendingOrder = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.ConversationId == conversationId
                                   && o.Status == InstaVende.Core.Enums.OrderStatus.Pending);
        if (pendingOrder != null)
        {
            _db.OrderItems.RemoveRange(pendingOrder.Items);
            _db.Orders.Remove(pendingOrder);
            await _db.SaveChangesAsync();
        }
        return "🗑️ Tu carrito ha sido vaciado.\n\nEscribe *#catalogo* para ver nuestros productos. 😊";
    }

    private async Task<string> GetOrderStatusAsync(int conversationId, BotConfig cfg)
    {
        var lastOrder = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.ConversationId == conversationId
                     && o.Status != InstaVende.Core.Enums.OrderStatus.Pending)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastOrder == null)
            return "📊 No tienes pedidos registrados todavía.\n\nEscribe *#catalogo* para ver nuestros productos. 😊";

        var statusLabel = lastOrder.Status switch
        {
            InstaVende.Core.Enums.OrderStatus.Confirmed  => "✅ Confirmado",
            InstaVende.Core.Enums.OrderStatus.Preparing  => "⚙️ En preparación",
            InstaVende.Core.Enums.OrderStatus.Shipped    => "🚚 Enviado",
            InstaVende.Core.Enums.OrderStatus.Delivered  => "📬 Entregado",
            InstaVende.Core.Enums.OrderStatus.Cancelled  => "❌ Cancelado",
            _                                             => lastOrder.Status.ToString()
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📊 *ESTADO DE TU PEDIDO #{lastOrder.OrderNumber}*");
        sb.AppendLine(new string('─', 26));
        sb.AppendLine($"Estado: {statusLabel}");
        sb.AppendLine($"Fecha: {lastOrder.CreatedAt:dd/MM/yyyy}");
        foreach (var item in lastOrder.Items)
            sb.AppendLine($"• {item.Product.Name} × {item.Quantity}");
        sb.AppendLine($"\n💰 Total: S/ {lastOrder.Total:N2}");
        return sb.ToString();
    }

    private async Task<string> AddToCartFromMessageAsync(int businessId, int conversationId, string msg, BotConfig cfg)
    {
        // Extract product number from message: "agregar 1", "quiero el 2", etc.
        var match = System.Text.RegularExpressions.Regex.Match(msg, @"\d+");
        if (!match.Success || !int.TryParse(match.Value, out var num) || num < 1)
            return "🤔 No entendí el número del producto. Escribe *#catalogo* para ver la lista numerada.";

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.BusinessId == businessId && p.IsActive && p.Stock > 0)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Take(20).ToListAsync();

        if (num > products.Count)
            return $"❌ No existe el producto número {num}. Escribe *#catalogo* para ver los disponibles.";

        var product = products[num - 1];

        // Find or create pending order (acting as cart)
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.ConversationId == conversationId
                                   && o.BusinessId == businessId
                                   && o.Status == InstaVende.Core.Enums.OrderStatus.Pending);
        if (order == null)
        {
            var conv = await _db.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conv == null || conv.ContactId == 0)
                return "❌ No se pudo crear el carrito. Por favor intenta de nuevo.";

            order = new Order
            {
                BusinessId     = businessId,
                ContactId      = conv.ContactId,
                ConversationId = conversationId,
                OrderNumber    = $"CART-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Status         = InstaVende.Core.Enums.OrderStatus.Pending,
                SubStatus      = InstaVende.Core.Enums.OrderSubStatus.EnValidacion,
                Total          = 0,
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
        }

        var existing = order.Items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing != null)
            existing.Quantity++;
        else
            _db.OrderItems.Add(new OrderItem
            {
                OrderId     = order.Id,
                ProductId   = product.Id,
                ProductName = product.Name,
                UnitPrice   = product.Price,
                Quantity    = 1
            });

        await _db.SaveChangesAsync();

        return $"✅ *{product.Name}* agregado al carrito.\n" +
               $"💰 Precio: S/ {product.Price:N2}\n\n" +
               "🛒 Escribe *#carrito* para ver tu carrito\n" +
               "📦 Escribe *#pedido* para confirmar tu compra";
    }


    // ── OpenAI call with tool-calling loop ────────────────────────────────────

    private async Task<string> CallOpenAiWithToolsAsync(
        BotConfig cfg,
        int businessId,
        int conversationId,
        string userMsg,
        string apiKey,
        List<KnowledgeEntry> knowledgeEntries)
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
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAt)
            .Take(MaxHistoryMessages)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        // Build placeholder values from real data
        var now = DateTime.UtcNow;
        var vendorConfig = cfg.Business.VendorConfig;
        var placeholders = BuildPlaceholders(cfg, vendorConfig, contact, conversation, isReturning, now, knowledgeEntries);

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

        // Configure LLM options — loaded from MasterBot_LLMConfig.json
        var llmCfg = _promptBuilder.LoadLlmConfig();
        var options = new ChatCompletionOptions
        {
            Temperature        = llmCfg.Temperature,
            MaxOutputTokenCount = llmCfg.MaxTokens,
            TopP               = llmCfg.TopP,
            FrequencyPenalty   = llmCfg.FrequencyPenalty,
            PresencePenalty    = llmCfg.PresencePenalty,
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

                var handoffTriggered = false;
                foreach (var toolCall in completion.ToolCalls)
                {
                    var toolResult = await ExecuteToolCallAsync(toolCall, businessId, conversationId, cfg);
                    _logger.LogDebug("Tool {Name} executed for business {Id}", toolCall.FunctionName, businessId);
                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, toolResult));

                    // Stop the loop immediately after handoff — no more AI turns needed
                    if (toolCall.FunctionName == "transferir_a_humano")
                    {
                        handoffTriggered = true;
                        break;
                    }
                }
                if (handoffTriggered) break;
            }

            iteration++;
        }
        while (completion.FinishReason == ChatFinishReason.ToolCalls && iteration < MaxToolIterations);

        return completion.Content.FirstOrDefault()?.Text ?? cfg.FallbackMessage;
    }

    // ── Placeholder builder ───────────────────────────────────────────────────

    private static Dictionary<string, string> BuildPlaceholders(
        BotConfig cfg,
        VendorConfig? vendor,
        Contact? contact,
        Conversation? conversation,
        bool isReturning,
        DateTime now,
        List<KnowledgeEntry>? knowledgeEntries = null)
    {
        // Build knowledge base context block from KnowledgeEntries
        var kbBlock = string.Empty;
        if (knowledgeEntries != null && knowledgeEntries.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var grp in knowledgeEntries.GroupBy(k => k.Category))
            {
                sb.AppendLine($"## {grp.Key}");
                foreach (var entry in grp)
                    sb.AppendLine($"- **{entry.Title}**: {entry.Content}");
            }
            kbBlock = sb.ToString();
        }

        // Build vendor personality block from VendorConfig
        var vendorPersonality = string.Empty;
        if (vendor != null)
        {
            var vb = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(vendor.VendorName))
                vb.AppendLine($"Tu nombre es {vendor.VendorName}{(vendor.VendorGender != null ? $" ({vendor.VendorGender})" : "")}.");
            if (!string.IsNullOrWhiteSpace(vendor.BusinessDescription))
                vb.AppendLine($"El negocio: {vendor.BusinessDescription}");
            if (!string.IsNullOrWhiteSpace(vendor.TargetAudience))
                vb.AppendLine($"Público objetivo: {vendor.TargetAudience}");
            if (!string.IsNullOrWhiteSpace(vendor.CommunicationStyle))
                vb.AppendLine($"Estilo de comunicación: {vendor.CommunicationStyle}");
            if (!string.IsNullOrWhiteSpace(vendor.SalesStyle))
                vb.AppendLine($"Estilo de ventas: {vendor.SalesStyle}");
            if (!string.IsNullOrWhiteSpace(vendor.Rules))
                vb.AppendLine($"Reglas adicionales:\n{vendor.Rules}");
            if (!string.IsNullOrWhiteSpace(vendor.WordsToAvoid))
                vb.AppendLine($"Palabras o frases a EVITAR: {vendor.WordsToAvoid}");
            if (!string.IsNullOrWhiteSpace(vendor.WelcomeMessage))
                vb.AppendLine($"Mensaje de bienvenida personalizado: {vendor.WelcomeMessage}");
            vb.AppendLine($"Usar emojis: {(vendor.UseEmojis ? "sí" : "no")}.");
            vb.AppendLine($"Longitud de respuesta: máximo {vendor.ResponseLength} oraciones por mensaje.");
            vendorPersonality = vb.ToString();
        }

        var botName = vendor?.VendorName ?? cfg.BotName;

        return new Dictionary<string, string>
        {
            ["BOT_NAME"]               = botName,
            ["VENDOR_PERSONALITY"]     = vendorPersonality,
            ["KNOWLEDGE_BASE"]         = kbBlock,
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
            ["TOTAL_PURCHASES"]        = (contact?.TotalPurchases ?? 0).ToString(),
            ["LAST_PURCHASE_SUMMARY"]  = contact?.LastPurchaseAt.HasValue == true
                                            ? $"Última compra el {contact.LastPurchaseAt.Value:dd/MM/yyyy}. Total acumulado: S/ {contact.TotalSpent:N2}"
                                            : "Sin compras previas",
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
            ["SHIPPING_ZONES"]         = "Consulta con nuestro equipo las zonas de envío disponibles.",
            ["PAYMENT_METHODS"]        = cfg.PaymentMethods   ?? "Tarjeta, transferencia y efectivo.",
            ["BUSINESS_HOURS"]         = cfg.BusinessHours    ?? "Lunes a Viernes 9:00-18:00.",
            ["AGENTS_AVAILABLE"]       = "1",
            ["ESTIMATED_WAIT_TIME"]    = "5",
            ["IS_WITHIN_BUSINESS_HOURS"] = IsWithinBusinessHours(cfg.BusinessHours, now).ToString().ToLower(),
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
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ExternalId == userId && c.BusinessId == businessId);

        if (contact == null)
            return Serialize(new { historial = Array.Empty<object>(), total_conversaciones = 0 });

        var totalConvs = await _db.Conversations.CountAsync(c => c.ContactId == contact.Id);
        var ultimaConv = await _db.Conversations
            .AsNoTracking()
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

    /// <summary>
    /// Parses "HH:mm-HH:mm" (e.g. "09:00-18:00") from BusinessHours and returns
    /// whether the given UTC time falls within that window.
    /// Falls back to true if the format cannot be parsed.
    /// </summary>
    // TZ id for Peru (UTC-5, no DST)
    private static readonly TimeZoneInfo _limaTz =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SA Pacific Standard Time" : "America/Lima");

    private static bool IsWithinBusinessHours(string? businessHours, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(businessHours)) return true;
        // Expect format: "Lunes a Viernes 9:00-18:00." or bare "09:00-18:00"
        var match = System.Text.RegularExpressions.Regex.Match(
            businessHours, @"(\d{1,2}):(\d{2})\s*[-–]\s*(\d{1,2}):(\d{2})");
        if (!match.Success) return true;

        var startH = int.Parse(match.Groups[1].Value);
        var startM = int.Parse(match.Groups[2].Value);
        var endH   = int.Parse(match.Groups[3].Value);
        var endM   = int.Parse(match.Groups[4].Value);

        // Convert UTC to Lima local time before comparing
        var localNow    = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _limaTz);
        var minuteNow   = localNow.Hour * 60 + localNow.Minute;
        var minuteStart = startH * 60 + startM;
        var minuteEnd   = endH   * 60 + endM;

        return minuteNow >= minuteStart && minuteNow < minuteEnd;
    }

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

