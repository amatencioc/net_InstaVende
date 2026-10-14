using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InstaVende.Web.Services.Messaging;

/// <summary>Configuración de un canal con los secretos ya descifrados (solo en memoria).</summary>
public sealed record ChannelSnapshot(
    int BusinessId, ChannelType Channel, bool IsActive,
    string? AccessToken, string? AppSecret, string? WebhookVerifyToken,
    string? PhoneNumberId, string? PageId, string? InstagramAccountId)
{
    /// <summary>WhatsApp vía Cloud API oficial (tiene PhoneNumberId y token); si no, sesión QR de wa-client.</summary>
    public bool IsWhatsAppCloud => Channel == ChannelType.WhatsApp && !string.IsNullOrEmpty(PhoneNumberId) && !string.IsNullOrEmpty(AccessToken);
}

/// <summary>
/// Lee <c>ChannelConfig</c> con caché de 5 minutos para no consultar la BD ni descifrar
/// tokens en cada mensaje. Invalidar con <see cref="Invalidate"/> al guardar o desconectar un canal.
/// </summary>
public class ChannelConfigProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly DataProtectionService _dp;

    public ChannelConfigProvider(AppDbContext db, IMemoryCache cache, DataProtectionService dp)
    {
        _db = db;
        _cache = cache;
        _dp = dp;
    }

    private static string Key(int businessId, ChannelType channel) => $"ChannelCfg_{businessId}_{(int)channel}";

    public async Task<ChannelSnapshot?> GetAsync(int businessId, ChannelType channel, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(Key(businessId, channel), out ChannelSnapshot? cached)) return cached;

        var cfg = await _db.ChannelConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.ChannelType == channel, ct);

        var snapshot = cfg == null ? null : new ChannelSnapshot(
            cfg.BusinessId, cfg.ChannelType, cfg.IsActive,
            Decrypt(cfg.AccessTokenEncrypted), Decrypt(cfg.AppSecretEncrypted), cfg.WebhookVerifyToken,
            cfg.PhoneNumberId, cfg.PageId, cfg.InstagramAccountId);

        _cache.Set(Key(businessId, channel), snapshot, Ttl);
        return snapshot;
    }

    public void Invalidate(int businessId)
    {
        foreach (var channel in Enum.GetValues<ChannelType>()) _cache.Remove(Key(businessId, channel));
    }

    private string? Decrypt(string? cipher) => string.IsNullOrWhiteSpace(cipher) ? null : _dp.TryDecrypt(cipher);
}
