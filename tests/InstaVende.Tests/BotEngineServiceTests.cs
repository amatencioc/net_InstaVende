using FluentAssertions;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace InstaVende.Tests;

public class BotEngineServiceTests
{
    private AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private BotEngineService CreateService(AppDbContext db, string? openAiKey = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:ApiKey"] = openAiKey })
            .Build();
        var logger = Mock.Of<ILogger<BotEngineService>>();
        return new BotEngineService(db, config, logger);
    }

    [Fact]
    public async Task ProcessMessage_NoBotConfig_ReturnsDefault()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var result = await svc.ProcessMessageAsync(1, 1, "hola");
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessMessage_InactiveBot_ReturnsDefault()
    {
        using var db = CreateDb();
        db.BotConfigs.Add(new BotConfig
        {
            BusinessId = 1,
            IsActive = false,
            BotName = "Bot",
            FallbackMessage = "No disponible",
            Language = "es",
            InteractionLevel = InteractionLevel.Standard
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.ProcessMessageAsync(1, 1, "hola");
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessMessage_MatchesIntent_ReturnsIntentResponse()
    {
        using var db = CreateDb();
        var config = new BotConfig
        {
            BusinessId = 2,
            IsActive = true,
            BotName = "TestBot",
            FallbackMessage = "No entendí",
            Language = "es",
            InteractionLevel = InteractionLevel.Standard
        };
        db.BotConfigs.Add(config);
        await db.SaveChangesAsync();

        db.BotIntents.Add(new BotIntent
        {
            BotConfigId = config.Id,
            IntentName = "Saludo",
            TriggerPhrases = """["hola","buenos días"]""",
            Response = "¡Hola! Bienvenido.",
            IsActive = true,
            Priority = 0
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.ProcessMessageAsync(2, 1, "hola");
        result.Should().Be("¡Hola! Bienvenido.");
    }

    [Fact]
    public async Task ProcessMessage_HandoffTrigger_ReturnsHandoffMessage()
    {
        using var db = CreateDb();
        db.BotConfigs.Add(new BotConfig
        {
            BusinessId = 3,
            IsActive = true,
            BotName = "Bot",
            FallbackMessage = "No entendí",
            Language = "es",
            InteractionLevel = InteractionLevel.Standard,
            EnableHandoff = true,
            HandoffTriggerPhrase = "hablar con agente"
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.ProcessMessageAsync(3, 1, "quiero hablar con agente");
        result.Should().Contain("agente");
    }

    [Fact]
    public async Task ProcessMessage_KnowledgeBaseMatch_ReturnsAnswer()
    {
        using var db = CreateDb();
        var config = new BotConfig
        {
            BusinessId = 4,
            IsActive = true,
            BotName = "Bot",
            FallbackMessage = "No entendí",
            Language = "es",
            InteractionLevel = InteractionLevel.Standard
        };
        db.BotConfigs.Add(config);
        await db.SaveChangesAsync();

        db.BotKnowledges.Add(new BotKnowledge
        {
            BotConfigId = config.Id,
            Question = "horario",
            Answer = "Atendemos de 9am a 6pm",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.ProcessMessageAsync(4, 1, "cuál es su horario");
        result.Should().Be("Atendemos de 9am a 6pm");
    }

    [Fact]
    public async Task ProcessMessage_NoMatch_ReturnsFallback()
    {
        using var db = CreateDb();
        db.BotConfigs.Add(new BotConfig
        {
            BusinessId = 5,
            IsActive = true,
            BotName = "Bot",
            FallbackMessage = "No puedo ayudarte con eso.",
            Language = "es",
            InteractionLevel = InteractionLevel.Standard
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.ProcessMessageAsync(5, 1, "pregunta sin respuesta xyz123");
        result.Should().Be("No puedo ayudarte con eso.");
    }
}
