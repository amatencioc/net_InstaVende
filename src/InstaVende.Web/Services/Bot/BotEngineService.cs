using System.Globalization;
using System.Text;
using InstaVende.Core.Domain;
using InstaVende.Core.Enums;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace InstaVende.Web.Services.Bot;

/// <summary>Cliente de OpenAI compartido (thread-safe). Null si no hay ApiKey configurada.</summary>
public sealed class OpenAiChatClientAccessor
{
    public ChatClient? Client { get; }

    public OpenAiChatClientAccessor(IOptions<OpenAiOptions> options)
    {
        var o = options.Value;
        Client = string.IsNullOrWhiteSpace(o.ApiKey) ? null : new ChatClient(o.Model, o.ApiKey);
    }
}

/// <summary>
/// Orquestador del vendedor IA: frase de derivación → comandos rápidos → respuestas
/// configuradas → modelo de lenguaje con herramientas.
/// </summary>
public class BotEngineService : IBotEngineService
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-PE");
    private static readonly TimeSpan LlmTimeout = TimeSpan.FromSeconds(45);

    private readonly AppDbContext _db;
    private readonly BotContextProvider _contexts;
    private readonly CartService _cart;
    private readonly BotToolExecutor _tools;
    private readonly BotPromptBuilder _prompts;
    private readonly OpenAiChatClientAccessor _llm;
    private readonly BotCacheOptions _opts;
    private readonly ILogger<BotEngineService> _logger;

    public BotEngineService(AppDbContext db, BotContextProvider contexts, CartService cart, BotToolExecutor tools,
        BotPromptBuilder prompts, OpenAiChatClientAccessor llm, IOptions<BotCacheOptions> opts, ILogger<BotEngineService> logger)
    {
        _db = db;
        _contexts = contexts;
        _cart = cart;
        _tools = tools;
        _prompts = prompts;
        _llm = llm;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<BotReply> ProcessMessageAsync(int businessId, int conversationId, string incomingMessage, CancellationToken ct = default)
    {
        var ctx = await _contexts.GetAsync(businessId, ct);
        if (ctx is not { IsActive: true }) return BotReply.Silent;

        var msg = incomingMessage.Trim();
        if (msg.Length == 0) return BotReply.Silent;

        var customer = conversationId > 0 ? await LoadCustomerAsync(businessId, conversationId, ct) : null;
        if (conversationId > 0 && customer == null) return BotReply.Silent; // conversación ajena o inexistente

        // ── 1. Frase de derivación configurada ───────────────────────────────
        if (ctx.EnableHandoff && ctx.HandoffTriggerPhrase != null && BotCommandRouter.ContainsPhrase(msg, ctx.HandoffTriggerPhrase))
            return await HandoffAsync(ctx, conversationId, ct);

        // ── 2. Comandos rápidos (#catalogo, #carrito, agregar 2…) ────────────
        var (command, number, quantity) = BotCommandRouter.Detect(msg);
        if (command != BotCommand.None)
            return new BotReply(await RunCommandAsync(ctx, conversationId, command, number, quantity, ct));

        // ── 3. Respuestas configuradas por el comercio ───────────────────────
        var intent = ctx.Intents.FirstOrDefault(i => i.Phrases.Any(p => BotCommandRouter.ContainsPhrase(msg, p)));
        if (intent != null) return new BotReply(intent.Response);

        // ── 4. Modelo de lenguaje ────────────────────────────────────────────
        if (_llm.Client == null)
        {
            var quick = ctx.QuickAnswers.FirstOrDefault(q => BotCommandRouter.ContainsPhrase(msg, q.Question));
            return new BotReply(quick?.Answer ?? ctx.FallbackMessage);
        }

        try
        {
            return await RunLlmAsync(_llm.Client, ctx, conversationId, customer, msg, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error del modelo de lenguaje (negocio {BusinessId}, conversación {ConversationId})", businessId, conversationId);
            return new BotReply(ctx.FallbackMessage);
        }
    }

    // ── IA con herramientas ──────────────────────────────────────────────────

    private async Task<BotReply> RunLlmAsync(ChatClient client, BotContext ctx, int conversationId, CustomerSnapshot? customer,
        string userMessage, CancellationToken ct)
    {
        var cart = conversationId > 0 ? await _cart.GetCartAsync(ctx.BusinessId, conversationId, ct) : null;
        var systemPrompt = _prompts.BuildSystemPrompt(ctx.StaticPlaceholders, BuildDynamicValues(ctx, customer, cart));

        var messages = new List<ChatMessage> { ChatMessage.CreateSystemMessage(systemPrompt) };
        messages.AddRange(await LoadHistoryAsync(conversationId, userMessage, ct));
        messages.Add(ChatMessage.CreateUserMessage(userMessage));

        var llm = _prompts.LlmConfig;
        var options = new ChatCompletionOptions
        {
            Temperature         = llm.Temperature,
            MaxOutputTokenCount = llm.MaxTokens,
            TopP                = llm.TopP,
            FrequencyPenalty    = llm.FrequencyPenalty,
            PresencePenalty     = llm.PresencePenalty,
        };
        foreach (var tool in _prompts.Tools) options.Tools.Add(tool);

        var toolCtx = new ToolContext { Bot = ctx, ConversationId = conversationId, ContactId = customer?.ContactId };
        var maxIterations = Math.Max(1, _opts.MaxToolIterations);

        for (var iteration = 0; ; iteration++)
        {
            // En la última vuelta se obliga al modelo a responder con texto
            if (options.Tools.Count > 0)
                options.ToolChoice = iteration >= maxIterations ? ChatToolChoice.CreateNoneChoice() : ChatToolChoice.CreateAutoChoice();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(LlmTimeout);
            ChatCompletion completion = await client.CompleteChatAsync(messages, options, timeout.Token);

            if (completion.FinishReason == ChatFinishReason.ToolCalls && completion.ToolCalls.Count > 0)
            {
                messages.Add(new AssistantChatMessage(completion));
                foreach (var call in completion.ToolCalls)
                    messages.Add(ChatMessage.CreateToolMessage(call.Id, await _tools.ExecuteAsync(call, toolCtx, ct)));

                if (toolCtx.HandoffTriggered)
                {
                    _logger.LogInformation("Derivación a humano solicitada por el bot (conversación {ConversationId}): {Summary}",
                        conversationId, toolCtx.HandoffSummary);
                    return await HandoffAsync(ctx, conversationId, ct);
                }
                continue;
            }

            if (completion.FinishReason == ChatFinishReason.ContentFilter)
                return new BotReply(ctx.FallbackMessage);

            var text = string.Concat(completion.Content.Where(p => p.Kind == ChatMessageContentPartKind.Text).Select(p => p.Text)).Trim();
            if (completion.FinishReason == ChatFinishReason.Length)
                _logger.LogWarning("Respuesta del bot truncada por max_tokens (conversación {ConversationId})", conversationId);
            return new BotReply(text.Length > 0 ? text : ctx.FallbackMessage);
        }
    }

    private async Task<IEnumerable<ChatMessage>> LoadHistoryAsync(int conversationId, string currentMessage, CancellationToken ct)
    {
        if (conversationId <= 0) return [];

        var history = await _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId
                     && m.Content != ""
                     && m.DeliveryStatus != MessageDeliveryStatus.Failed)
            .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Id)
            .Take(Math.Max(1, _opts.MaxHistoryMessages) + 1)
            .Select(m => new { m.Direction, m.Content })
            .ToListAsync(ct);
        history.Reverse();

        // El mensaje actual ya está persistido: se quita para no enviarlo dos veces.
        if (history.Count > 0 && history[^1].Direction == MessageDirection.Inbound && history[^1].Content.Trim() == currentMessage)
            history.RemoveAt(history.Count - 1);
        if (history.Count > _opts.MaxHistoryMessages) history.RemoveAt(0);

        return history.Select(m => m.Direction == MessageDirection.Inbound
            ? (ChatMessage)ChatMessage.CreateUserMessage(m.Content)
            : ChatMessage.CreateAssistantMessage(m.Content));
    }

    private static Dictionary<string, string> BuildDynamicValues(BotContext ctx, CustomerSnapshot? customer, Core.Entities.Order? cart)
    {
        var local = BusinessLocale.ToLocal(DateTime.UtcNow, ctx.Country);
        var open = BusinessHours.IsOpen(ctx.BusinessHours, local);
        return new Dictionary<string, string>
        {
            ["CURRENT_DATE"]             = local.ToString("dd/MM/yyyy", Es),
            ["CURRENT_TIME"]             = local.ToString("HH:mm", Es),
            ["DAY_OF_WEEK"]              = Es.DateTimeFormat.GetDayName(local.DayOfWeek),
            ["IS_WEEKEND"]               = local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "sí" : "no",
            ["IS_WITHIN_BUSINESS_HOURS"] = open switch { true => "sí", false => "no", null => "desconocido" },
            ["CHANNEL"]                  = customer?.Channel switch
            {
                ChannelType.WhatsApp          => "WhatsApp",
                ChannelType.Instagram         => "Instagram",
                ChannelType.FacebookMessenger => "Facebook Messenger",
                _                             => "Vista previa del panel",
            },
            ["USER_NAME"]                = SanitizeName(customer?.Name) ?? "(desconocido)",
            ["IS_RETURNING_USER"]        = customer?.IsReturning == true ? "sí" : "no",
            ["TOTAL_PURCHASES"]          = (customer?.TotalPurchases ?? 0).ToString(Es),
            ["LAST_PURCHASE_SUMMARY"]    = customer?.LastPurchaseAt is DateTime last
                ? $"{BusinessLocale.ToLocal(last, ctx.Country):dd/MM/yyyy} (total acumulado {ctx.Money(customer.TotalSpent)})"
                : "Sin compras previas",
            ["CART_SUMMARY"]             = CartService.Summarize(cart, ctx),
        };
    }

    /// <summary>
    /// El nombre lo controla el cliente (pushname de WhatsApp): se limita y se le quitan
    /// caracteres que podrían usarse para inyectar instrucciones en el prompt.
    /// </summary>
    private static string? SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var clean = new string(name.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '.' or '\'' or '-').ToArray()).Trim();
        if (clean.Length == 0) return null;
        return clean.Length > 40 ? clean[..40] : clean;
    }

    // ── Derivación ───────────────────────────────────────────────────────────

    private async Task<BotReply> HandoffAsync(BotContext ctx, int conversationId, CancellationToken ct)
    {
        if (conversationId > 0 && ctx.AutoPauseOnHandoff)
        {
            await _db.Conversations
                .Where(c => c.Id == conversationId && c.BusinessId == ctx.BusinessId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, ConversationStatus.WaitingHuman)
                    .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), ct);
        }
        return new BotReply(ctx.HandoffMessage, HandoffTriggered: true);
    }

    // ── Comandos rápidos ─────────────────────────────────────────────────────

    private async Task<string> RunCommandAsync(BotContext ctx, int conversationId, BotCommand command, int number, int quantity, CancellationToken ct)
    {
        const string previewOnly = "Esta función solo está disponible en una conversación real con un cliente.";
        switch (command)
        {
            case BotCommand.Help:
                return "🤖 *Comandos disponibles*\n" +
                       "📋 *#catalogo* → Ver productos\n" +
                       "🛒 *#carrito* → Ver tu carrito\n" +
                       "✅ *#pedido* → Confirmar tu pedido\n" +
                       "🗑️ *#limpiar* → Vaciar el carrito\n" +
                       "📦 *#estado* → Estado de tu último pedido\n\n" +
                       "También puedes escribirme con normalidad 😊";

            case BotCommand.Catalog:
            {
                var products = await CatalogAsync(ctx.BusinessId, ct);
                if (products.Count == 0) return "😔 No tenemos productos disponibles en este momento.";
                var sb = new StringBuilder($"📋 *Catálogo de {ctx.BusinessName}*\n");
                for (var i = 0; i < products.Count; i++)
                {
                    var p = products[i];
                    sb.Append($"\n*{i + 1}. {p.Name}* — {ctx.Money(p.Price)}");
                    sb.Append(p.Stock > 5 ? "" : $" (solo {p.Stock} disponibles)");
                }
                sb.Append("\n\nEscribe *agregar N* para añadir un producto (ej. *agregar 1*).");
                return sb.ToString();
            }

            case BotCommand.AddToCart:
            {
                if (conversationId <= 0) return previewOnly;
                var products = await CatalogAsync(ctx.BusinessId, ct);
                if (number < 1 || number > products.Count)
                    return $"No existe el producto número {number}. Escribe *#catalogo* para ver la lista.";
                var result = await _cart.AddItemAsync(ctx, conversationId, products[number - 1].Id, quantity, null, ct);
                if (!result.Success) return $"⚠️ {result.Message}";
                return $"✅ {result.Message}\n\n🛒 *#carrito* para ver tu carrito\n✅ *#pedido* para confirmar";
            }

            case BotCommand.Cart:
            {
                if (conversationId <= 0) return previewOnly;
                var cart = await _cart.GetCartAsync(ctx.BusinessId, conversationId, ct);
                if (cart == null || cart.Items.Count == 0) return "🛒 Tu carrito está vacío. Escribe *#catalogo* para ver los productos.";
                return $"🛒 *Tu carrito*\n{CartService.Summarize(cart, ctx)}\n\n✅ *#pedido* para confirmar · 🗑️ *#limpiar* para vaciar";
            }

            case BotCommand.ClearCart:
                if (conversationId <= 0) return previewOnly;
                await _cart.ClearAsync(ctx.BusinessId, conversationId, ct);
                return "🗑️ Tu carrito quedó vacío.";

            case BotCommand.Order:
            {
                if (conversationId <= 0) return previewOnly;
                var result = await _cart.ConfirmAsync(ctx, conversationId, null, null, null, null, ct);
                if (!result.Success || result.Order == null)
                    return result.Message == "El carrito está vacío." ? "🛒 Tu carrito está vacío. Escribe *#catalogo* para ver los productos." : $"⚠️ {result.Message}";
                var o = result.Order;
                return $"✅ *Pedido {o.OrderNumber} registrado*\n" +
                       string.Join("\n", o.Items.Select(i => $"• {i.ProductName} × {i.Quantity}")) +
                       $"\n💰 Total: {ctx.Money(o.Total)}\n\n{ctx.PurchaseConfirmationMessage}\nEn breve te contactamos para coordinar el pago y la entrega.";
            }

            case BotCommand.Status:
            {
                if (conversationId <= 0) return previewOnly;
                var contactId = await _db.Conversations.Where(c => c.Id == conversationId).Select(c => c.ContactId).FirstAsync(ct);
                var last = await _db.Orders.AsNoTracking()
                    .Where(o => o.BusinessId == ctx.BusinessId && o.ContactId == contactId && o.Status != OrderStatus.Cart)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new { o.OrderNumber, o.SubStatus, o.Total, o.CreatedAt })
                    .FirstOrDefaultAsync(ct);
                if (last == null) return "📦 Aún no tienes pedidos registrados.";
                return $"📦 *Pedido {last.OrderNumber}*\nEstado: {OrderWorkflow.Label(last.SubStatus)}\n" +
                       $"Fecha: {BusinessLocale.ToLocal(last.CreatedAt, ctx.Country):dd/MM/yyyy}\nTotal: {ctx.Money(last.Total)}";
            }

            default:
                return ctx.FallbackMessage;
        }
    }

    private Task<List<CatalogItem>> CatalogAsync(int businessId, CancellationToken ct)
        => _db.Products.AsNoTracking()
            .Where(p => p.BusinessId == businessId && p.IsActive && p.Stock > 0)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ThenBy(p => p.Id)
            .Take(20)
            .Select(p => new CatalogItem(p.Id, p.Name, p.Price, p.Stock))
            .ToListAsync(ct);

    private Task<CustomerSnapshot?> LoadCustomerAsync(int businessId, int conversationId, CancellationToken ct)
        => _db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId && c.BusinessId == businessId)
            .Select(c => new CustomerSnapshot(
                c.ContactId, c.ChannelType, c.Contact.Name, c.Contact.TotalPurchases, c.Contact.TotalSpent, c.Contact.LastPurchaseAt,
                c.Contact.TotalPurchases > 0 || c.Contact.Conversations.Any(o => o.Id != conversationId)))
            .FirstOrDefaultAsync(ct);

    private sealed record CatalogItem(int Id, string Name, decimal Price, int Stock);

    private sealed record CustomerSnapshot(int ContactId, ChannelType Channel, string? Name, int TotalPurchases,
        decimal TotalSpent, DateTime? LastPurchaseAt, bool IsReturning);
}
