using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services.Channels;
using InstaVende.Web.Services.Messaging;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Channels;

/// <summary>
/// Persistencia de <see cref="ChannelConfig"/> para los controladores del módulo.
/// Toda escritura invalida la caché de <see cref="ChannelConfigProvider"/> del negocio.
/// Se instancia por petición desde los controladores (no está registrado en DI).
/// </summary>
internal sealed class ChannelStore(AppDbContext db, ChannelConfigProvider provider, ILogger logger)
{
    public Task<ChannelConfig?> FindAsync(int businessId, ChannelType channel, CancellationToken ct)
        => db.ChannelConfigs.FirstOrDefaultAsync(c => c.BusinessId == businessId && c.ChannelType == channel, ct);

    public Task<List<ChannelConfig>> ListAsync(int businessId, CancellationToken ct)
        => db.ChannelConfigs.AsNoTracking().Where(c => c.BusinessId == businessId).ToListAsync(ct);

    /// <summary>Crea (sin guardar) la configuración del canal, inactiva y con un verify token nuevo.</summary>
    public ChannelConfig Create(int businessId, ChannelType channel)
    {
        var cfg = new ChannelConfig
        {
            BusinessId           = businessId,
            ChannelType          = channel,
            AccessTokenEncrypted = string.Empty,
            WebhookVerifyToken   = ChannelConfigRules.NewVerifyToken(),
            IsActive             = false,
            CreatedAt            = DateTime.UtcNow,
        };
        db.ChannelConfigs.Add(cfg);
        return cfg;
    }

    /// <summary>Marca el paso "canal conectado" del onboarding (idempotente).</summary>
    public async Task MarkChannelConnectedAsync(int businessId, CancellationToken ct)
    {
        var progress = await db.OnboardingProgresses.FirstOrDefaultAsync(o => o.BusinessId == businessId, ct);
        if (progress == null)
            db.OnboardingProgresses.Add(new OnboardingProgress { BusinessId = businessId, ChannelConnected = true });
        else if (!progress.ChannelConnected)
        {
            progress.ChannelConnected = true;
            progress.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Guarda los cambios pendientes. Devuelve false si chocó con una escritura concurrente
    /// (índice único BusinessId+ChannelType u onboarding); en ese caso descarta los cambios rastreados.
    /// </summary>
    public async Task<bool> SaveAsync(int businessId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Conflicto al guardar la configuración de canales del negocio {BusinessId}", businessId);
            db.ChangeTracker.Clear();
            return false;
        }
        finally
        {
            provider.Invalidate(businessId);
        }
    }

    /// <summary>
    /// Registra la sesión QR conectada: activa WhatsApp con el teléfono que informa wa-client
    /// (nunca el cliente), deja el modo QR (sin token de Cloud API) y marca el onboarding.
    /// Idempotente: si ya está todo al día no escribe. Reintenta una vez ante una escritura concurrente.
    /// </summary>
    public async Task<ChannelConfig?> SyncQrConnectedAsync(int businessId, WaSessionStatus status, CancellationToken ct)
    {
        var phone = ChannelConfigRules.NormalizePhone(status.Info?.Phone) ?? ChannelConfigRules.NormalizePhone(status.Info?.Wid);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var cfg = await FindAsync(businessId, ChannelType.WhatsApp, ct);

            // Con la Cloud API activa la sesión QR no toma el control del canal.
            if (ChannelConfigRules.IsWhatsAppCloudActive(cfg)) return cfg;

            var upToDate = cfg is { IsActive: true, ConnectedAt: not null }
                           && string.IsNullOrEmpty(cfg.AccessTokenEncrypted)
                           && !string.IsNullOrEmpty(cfg.WebhookVerifyToken)
                           && (phone == null || cfg.PhoneNumber == phone);
            if (upToDate) return cfg;

            cfg ??= Create(businessId, ChannelType.WhatsApp);
            var activating = !cfg.IsActive || cfg.ConnectedAt == null;
            cfg.IsActive = true;
            cfg.AccessTokenEncrypted = string.Empty; // modo QR: sin token, los envíos van por wa-client
            if (phone != null) cfg.PhoneNumber = phone;
            if (string.IsNullOrEmpty(cfg.WebhookVerifyToken)) cfg.WebhookVerifyToken = ChannelConfigRules.NewVerifyToken();
            if (activating)
            {
                cfg.ConnectedAt = DateTime.UtcNow;
                await MarkChannelConnectedAsync(businessId, ct);
            }

            if (await SaveAsync(businessId, ct))
            {
                if (activating) logger.LogInformation("WhatsApp QR conectado para el negocio {BusinessId}", businessId);
                return cfg;
            }
        }
        return await db.ChannelConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.ChannelType == ChannelType.WhatsApp, ct);
    }

    /// <summary>Desactiva el canal conservando sus credenciales para poder reactivarlo.</summary>
    public static void Deactivate(ChannelConfig cfg)
    {
        cfg.IsActive = false;
        cfg.ConnectedAt = null;
    }
}
