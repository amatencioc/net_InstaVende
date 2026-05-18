using FluentAssertions;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        var logger = Mock.Of<ILogger<BotEngineService>>();

        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
        var promptBuilderLogger = Mock.Of<ILogger<MasterPromptBuilder>>();
        var promptBuilder  = new MasterPromptBuilder(env.Object, promptBuilderLogger);
        var cache          = new MemoryCache(new MemoryCacheOptions());
        var cacheOptions   = Options.Create(new BotCacheOptions());
        var openAiOptions  = Options.Create(new OpenAiOptions { ApiKey = openAiKey ?? string.Empty });
        var config         = new ConfigurationBuilder().Build();

        return new BotEngineService(db, config, promptBuilder, logger, cache, cacheOptions, openAiOptions);
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
        db.Businesses.Add(new Business { Id = 2, UserId = "user2", Name = "TestBiz2" });
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
        db.Businesses.Add(new Business { Id = 3, UserId = "user3", Name = "TestBiz3" });
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
        // BotEngineService loads cfg.Business — seed a minimal Business to prevent null-ref
        db.Businesses.Add(new Business { Id = 4, UserId = "user4", Name = "TestBiz4" });
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
        db.Businesses.Add(new Business { Id = 5, UserId = "user5", Name = "TestBiz5" });
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
