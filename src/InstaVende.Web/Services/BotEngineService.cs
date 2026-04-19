using InstaVende.Core.Entities;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;

namespace InstaVende.Web.Services;

public class BotEngineService : IBotEngineService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<BotEngineService> _logger;

    public BotEngineService(AppDbContext db, IConfiguration config, ILogger<BotEngineService> logger)
    {
        _db = db; _config = config; _logger = logger;
    }

    public async Task<string> ProcessMessageAsync(int businessId, int conversationId, string incomingMessage)
    {
        var botConfig = await _db.BotConfigs
            .Include(b => b.Intents.Where(i => i.IsActive))
            .Include(b => b.KnowledgeBase.Where(k => k.IsActive))
            .FirstOrDefaultAsync(b => b.BusinessId == businessId && b.IsActive);

        if (botConfig == null) return "Hola, ¿en qué puedo ayudarte?";

        if (botConfig.EnableHandoff && !string.IsNullOrWhiteSpace(botConfig.HandoffTriggerPhrase)
            && incomingMessage.Contains(botConfig.HandoffTriggerPhrase, StringComparison.OrdinalIgnoreCase))
            return "Transferiendo con un agente humano. Por favor espera.";

        foreach (var intent in botConfig.Intents.OrderByDescending(i => i.Priority))
        {
            try
            {
                var phrases = System.Text.Json.JsonSerializer.Deserialize<List<string>>(intent.TriggerPhrases) ?? new();
                if (phrases.Any(p => incomingMessage.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    return intent.Response;
            }
            catch { }
        }

        var apiKey = _config["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try { return await CallOpenAiAsync(botConfig, businessId, incomingMessage, apiKey); }
            catch (Exception ex) { _logger.LogError(ex, "OpenAI error for business {Id}", businessId); }
        }

        var match = botConfig.KnowledgeBase
            .FirstOrDefault(k => incomingMessage.Contains(k.Question, StringComparison.OrdinalIgnoreCase));
        return match?.Answer ?? botConfig.FallbackMessage;
    }

    private async Task<string> CallOpenAiAsync(BotConfig cfg, int businessId, string userMsg, string apiKey)
    {
        var products = await _db.Products.Where(p => p.BusinessId == businessId && p.IsActive).Take(20)
            .Select(p => $"- {p.Name}: ${p.Price} — {p.Description ?? "Sin descripción"}").ToListAsync();
        var knowledge = cfg.KnowledgeBase.Select(k => $"Q: {k.Question}\nA: {k.Answer}").ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Eres {cfg.BotName}, un asistente virtual de ventas.");
        if (!string.IsNullOrWhiteSpace(cfg.Personality)) sb.AppendLine($"Personalidad: {cfg.Personality}");
        if (!string.IsNullOrWhiteSpace(cfg.BaseSystemPrompt)) sb.AppendLine(cfg.BaseSystemPrompt);
        sb.AppendLine($"Idioma: {cfg.Language}");
        if (products.Count > 0) { sb.AppendLine("\nProductos disponibles:"); sb.AppendLine(string.Join("\n", products)); }
        if (knowledge.Count > 0) { sb.AppendLine("\nBase de conocimiento:"); sb.AppendLine(string.Join("\n\n", knowledge)); }
        sb.AppendLine($"\nSi no puedes responder, di: {cfg.FallbackMessage}");

        var client = new ChatClient("gpt-4o", apiKey);
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(sb.ToString()),
            ChatMessage.CreateUserMessage(userMsg)
        };
        var result = await client.CompleteChatAsync(messages);
        return result.Value.Content[0].Text;
    }
}
