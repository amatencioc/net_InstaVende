using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VSDiagnostics;

namespace BenchmarkSuite1;
[CPUUsageDiagnoser]
public class ResolvePlaceholdersBenchmark
{
    private AppDbContext _db = null !;
    private const int BusinessId = 1;
    // Regex estáticos compilados — candidatos a la optimización
    private static readonly Regex _defaultValueRegex = new(@"\{\{[^|}]+\|\s*""([^""]*)""\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex _unresolvedRegex = new(@"\{\{[^}]+\}\}", RegexOptions.Compiled);
    // Template realista con ~20 placeholders, defaults y campos sin resolver
    private const string PromptTemplate = """
        Eres {{VENDOR_NAME}}, asistente de {{COMPANY_NAME}} ({{COUNTRY}}).
        Género: {{VENDOR_GENDER | "neutral"}}.
        Descripción: {{COMPANY_DESCRIPTION}}
        Público: {{TARGET_AUDIENCE | "público general"}}.
        Reglas: {{RULES | "Sin reglas definidas"}}.
        Horario: {{BUSINESS_HOURS | "Sin horario"}}.
        Estilo: {{COMMUNICATION_STYLE}}. Ventas: {{SALES_STYLE}}.
        Respuesta: {{RESPONSE_LENGTH}}. Emojis: {{USE_EMOJIS}}.
        Paleta: {{EMOJI_PALETTE | "😊"}}. Evitar: {{WORDS_TO_AVOID | "ninguna"}}.
        Bienvenida: {{WELCOME_MESSAGE}}
        Contacto: {{CONTACT_NAME}} (recurrente: {{IS_RETURNING}}).
        Fecha: {{CURRENT_DATE}} {{CURRENT_TIME}}.
        Base conocimiento: {{KNOWLEDGE_BASE}}
        {{UNRESOLVED_1}} {{UNRESOLVED_2 | "default_val"}} {{UNRESOLVED_3}}
        """;
    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("ResolvePlaceholdersBenchmarkDb").Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _db.VendorConfigs.Add(new VendorConfig { Id = 1, BusinessId = BusinessId, VendorName = "Lucía", VendorGender = "femenino", Country = "Perú", BusinessDescription = "Tienda de electrónica con envíos a todo el país.", TargetAudience = "jóvenes adultos tecnológicos", Rules = "No ofrecer descuentos sin aprobación.", CommunicationStyle = "amigable", SalesStyle = "consultivo", ResponseLength = 3, UseEmojis = true, EmojiPalette = "😊✨🛒", WordsToAvoid = "problema, imposible", WelcomeMessage = "¡Hola! Soy Lucía, ¿en qué te puedo ayudar hoy?", });
        for (int i = 1; i <= 5; i++)
            _db.KnowledgeEntries.Add(new KnowledgeEntry { Id = i, BusinessId = BusinessId, Title = $"FAQ {i}", Content = $"Respuesta conocimiento {i}.", IsFavorite = i <= 2, Category = KnowledgeCategory.Otros, });
        _db.SaveChanges();
    }

    [GlobalCleanup]
    public void Cleanup() => _db.Dispose();
    // ────── BASELINE: carga de datos + Regex interpretados ──────
    [Benchmark(Baseline = true, Description = "Prompt: carga DB + Regex interpretados")]
    public async Task BuildPrompt_InterpretedRegex()
    {
        var vendor = await _db.VendorConfigs.AsNoTracking().FirstOrDefaultAsync(v => v.BusinessId == BusinessId);
        var knowledge = await _db.KnowledgeEntries.AsNoTracking().Where(k => k.BusinessId == BusinessId).OrderByDescending(k => k.IsFavorite).Select(k => k.Content).ToListAsync();
        var placeholders = new Dictionary<string, string>
        {
            ["VENDOR_NAME"] = vendor?.VendorName ?? string.Empty,
            ["VENDOR_GENDER"] = vendor?.VendorGender ?? string.Empty,
            ["COMPANY_NAME"] = "TechStore",
            ["COUNTRY"] = vendor?.Country ?? string.Empty,
            ["COMPANY_DESCRIPTION"] = vendor?.BusinessDescription ?? string.Empty,
            ["TARGET_AUDIENCE"] = vendor?.TargetAudience ?? string.Empty,
            ["RULES"] = vendor?.Rules ?? string.Empty,
            ["BUSINESS_HOURS"] = "Lunes a Viernes 9:00-18:00.",
            ["COMMUNICATION_STYLE"] = vendor?.CommunicationStyle ?? string.Empty,
            ["SALES_STYLE"] = vendor?.SalesStyle ?? string.Empty,
            ["RESPONSE_LENGTH"] = vendor?.ResponseLength.ToString() ?? string.Empty,
            ["USE_EMOJIS"] = vendor?.UseEmojis.ToString() ?? string.Empty,
            ["EMOJI_PALETTE"] = vendor?.EmojiPalette ?? string.Empty,
            ["WORDS_TO_AVOID"] = vendor?.WordsToAvoid ?? string.Empty,
            ["WELCOME_MESSAGE"] = vendor?.WelcomeMessage ?? string.Empty,
            ["CONTACT_NAME"] = "Carlos Quispe",
            ["IS_RETURNING"] = "true",
            ["CURRENT_DATE"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["CURRENT_TIME"] = DateTime.UtcNow.ToString("HH:mm"),
            ["KNOWLEDGE_BASE"] = string.Join(" | ", knowledge),
        };
        var _ = ResolveInterpreted(PromptTemplate, placeholders);
    }

    // ────── OPTIMIZADO: misma carga DB + Regex compilados ──────
    [Benchmark(Description = "Prompt: carga DB + Regex compilados")]
    public async Task BuildPrompt_CompiledRegex()
    {
        var vendor = await _db.VendorConfigs.AsNoTracking().FirstOrDefaultAsync(v => v.BusinessId == BusinessId);
        var knowledge = await _db.KnowledgeEntries.AsNoTracking().Where(k => k.BusinessId == BusinessId).OrderByDescending(k => k.IsFavorite).Select(k => k.Content).ToListAsync();
        var placeholders = new Dictionary<string, string>
        {
            ["VENDOR_NAME"] = vendor?.VendorName ?? string.Empty,
            ["VENDOR_GENDER"] = vendor?.VendorGender ?? string.Empty,
            ["COMPANY_NAME"] = "TechStore",
            ["COUNTRY"] = vendor?.Country ?? string.Empty,
            ["COMPANY_DESCRIPTION"] = vendor?.BusinessDescription ?? string.Empty,
            ["TARGET_AUDIENCE"] = vendor?.TargetAudience ?? string.Empty,
            ["RULES"] = vendor?.Rules ?? string.Empty,
            ["BUSINESS_HOURS"] = "Lunes a Viernes 9:00-18:00.",
            ["COMMUNICATION_STYLE"] = vendor?.CommunicationStyle ?? string.Empty,
            ["SALES_STYLE"] = vendor?.SalesStyle ?? string.Empty,
            ["RESPONSE_LENGTH"] = vendor?.ResponseLength.ToString() ?? string.Empty,
            ["USE_EMOJIS"] = vendor?.UseEmojis.ToString() ?? string.Empty,
            ["EMOJI_PALETTE"] = vendor?.EmojiPalette ?? string.Empty,
            ["WORDS_TO_AVOID"] = vendor?.WordsToAvoid ?? string.Empty,
            ["WELCOME_MESSAGE"] = vendor?.WelcomeMessage ?? string.Empty,
            ["CONTACT_NAME"] = "Carlos Quispe",
            ["IS_RETURNING"] = "true",
            ["CURRENT_DATE"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["CURRENT_TIME"] = DateTime.UtcNow.ToString("HH:mm"),
            ["KNOWLEDGE_BASE"] = string.Join(" | ", knowledge),
        };
        var _ = ResolveCompiled(PromptTemplate, placeholders);
    }

    // ────── Implementaciones de ResolvePlaceholders ──────
    private static string ResolveInterpreted(string template, Dictionary<string, string> values)
    {
        foreach (var kv in values)
            template = template.Replace($"{{{{{kv.Key}}}}}", kv.Value);
        template = Regex.Replace(template, @"\{\{[^|}]+\|\s*""([^""]*)""\s*\}\}", "$1");
        template = Regex.Replace(template, @"\{\{[^}]+\}\}", string.Empty);
        return template;
    }

    private static string ResolveCompiled(string template, Dictionary<string, string> values)
    {
        foreach (var kv in values)
            template = template.Replace($"{{{{{kv.Key}}}}}", kv.Value);
        template = _defaultValueRegex.Replace(template, "$1");
        template = _unresolvedRegex.Replace(template, string.Empty);
        return template;
    }
}