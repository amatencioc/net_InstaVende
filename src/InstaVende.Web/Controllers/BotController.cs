using InstaVende.Core.Entities;
using InstaVende.Core.Interfaces;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services;
using InstaVende.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Controllers;

[Authorize]
public class BotController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUserService _cu;
    private readonly IBotEngineService _engine;

    public BotController(AppDbContext db, CurrentUserService cu, IBotEngineService engine)
    { _db = db; _cu = cu; _engine = engine; }

    public async Task<IActionResult> Index()
    {
        var biz = await _cu.GetBusinessAsync();
        if (biz == null) return RedirectToAction("Register", "Account");
        var cfg = await _db.BotConfigs.Include(b => b.Intents).Include(b => b.KnowledgeBase).Include(b => b.Flows)
            .FirstOrDefaultAsync(b => b.BusinessId == biz.Id);
        if (cfg == null) { cfg = new BotConfig { BusinessId = biz.Id }; _db.BotConfigs.Add(cfg); await _db.SaveChangesAsync(); }
        ViewBag.Intents = cfg.Intents.OrderByDescending(i => i.Priority).Select(MapIntent).ToList();
        ViewBag.Knowledge = cfg.KnowledgeBase.OrderBy(k => k.Category).Select(MapKb).ToList();
        ViewBag.Flows = cfg.Flows.Select(f => new { f.Id, f.Name, f.Description, f.IsActive, f.IsEntryFlow }).ToList();
        return View(MapConfig(cfg));
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveConfig([FromBody] BotConfigViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();
        var cfg = await _db.BotConfigs.FirstOrDefaultAsync(b => b.BusinessId == bid);
        if (cfg == null) return NotFound();
        cfg.BotName = model.BotName; cfg.Personality = model.Personality; cfg.BaseSystemPrompt = model.BaseSystemPrompt;
        cfg.InteractionLevel = model.InteractionLevel; cfg.Language = model.Language; cfg.FallbackMessage = model.FallbackMessage;
        cfg.EnableHandoff = model.EnableHandoff; cfg.HandoffTriggerPhrase = model.HandoffTriggerPhrase; cfg.IsActive = model.IsActive;
        cfg.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "Configuración guardada." });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIntent([FromBody] BotIntentViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        var cfg = await _db.BotConfigs.FirstOrDefaultAsync(b => b.BusinessId == bid);
        if (cfg == null) return NotFound();
        if (model.Id == 0) _db.BotIntents.Add(new BotIntent { BotConfigId = cfg.Id, IntentName = model.IntentName, TriggerPhrases = model.TriggerPhrases, Response = model.Response, IsActive = model.IsActive, Priority = model.Priority });
        else { var i = await _db.BotIntents.FirstOrDefaultAsync(x => x.Id == model.Id && x.BotConfigId == cfg.Id); if (i == null) return NotFound(); i.IntentName = model.IntentName; i.TriggerPhrases = model.TriggerPhrases; i.Response = model.Response; i.IsActive = model.IsActive; i.Priority = model.Priority; }
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteIntent([FromBody] IdRequest req)
    {
        var bid = await _cu.GetBusinessIdAsync();
        var cfg = await _db.BotConfigs.FirstOrDefaultAsync(b => b.BusinessId == bid);
        var intent = await _db.BotIntents.FirstOrDefaultAsync(i => i.Id == req.Id && i.BotConfigId == cfg!.Id);
        if (intent == null) return NotFound();
        _db.BotIntents.Remove(intent); await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveKnowledge([FromBody] BotKnowledgeViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        var cfg = await _db.BotConfigs.FirstOrDefaultAsync(b => b.BusinessId == bid);
        if (cfg == null) return NotFound();
        if (model.Id == 0) _db.BotKnowledges.Add(new BotKnowledge { BotConfigId = cfg.Id, Question = model.Question, Answer = model.Answer, Category = model.Category, IsActive = model.IsActive });
        else { var k = await _db.BotKnowledges.FirstOrDefaultAsync(x => x.Id == model.Id && x.BotConfigId == cfg.Id); if (k == null) return NotFound(); k.Question = model.Question; k.Answer = model.Answer; k.Category = model.Category; k.IsActive = model.IsActive; }
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteKnowledge([FromBody] IdRequest req)
    {
        var bid = await _cu.GetBusinessIdAsync();
        var cfg = await _db.BotConfigs.FirstOrDefaultAsync(b => b.BusinessId == bid);
        var kb = await _db.BotKnowledges.FirstOrDefaultAsync(k => k.Id == req.Id && k.BotConfigId == cfg!.Id);
        if (kb == null) return NotFound();
        _db.BotKnowledges.Remove(kb); await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview([FromBody] BotPreviewMessageViewModel model)
    {
        var bid = await _cu.GetBusinessIdAsync();
        if (bid == null) return Unauthorized();
        var reply = await _engine.ProcessMessageAsync(bid.Value, 0, model.Message);
        return Json(new { reply });
    }

    private static BotConfigViewModel MapConfig(BotConfig c) => new() { Id = c.Id, BotName = c.BotName, Personality = c.Personality, BaseSystemPrompt = c.BaseSystemPrompt, InteractionLevel = c.InteractionLevel, Language = c.Language, FallbackMessage = c.FallbackMessage, EnableHandoff = c.EnableHandoff, HandoffTriggerPhrase = c.HandoffTriggerPhrase, IsActive = c.IsActive };
    private static BotIntentViewModel MapIntent(BotIntent i) => new() { Id = i.Id, IntentName = i.IntentName, TriggerPhrases = i.TriggerPhrases, Response = i.Response, IsActive = i.IsActive, Priority = i.Priority };
    private static BotKnowledgeViewModel MapKb(BotKnowledge k) => new() { Id = k.Id, Question = k.Question, Answer = k.Answer, Category = k.Category, IsActive = k.IsActive };
}

public record IdRequest(int Id);
