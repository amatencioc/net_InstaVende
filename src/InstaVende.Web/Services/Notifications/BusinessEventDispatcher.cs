using System.Net;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services.Notifications;

/// <summary>
/// Entrega los <see cref="BusinessEvent"/>: evento SignalR "businessEvent" para el panel abierto
/// y email a los destinatarios activos configurados en Configuración → Notificaciones.
/// </summary>
public sealed class BusinessEventDispatcher(
    BusinessEvents events,
    IServiceScopeFactory scopes,
    IHubContext<InboxHub> hub,
    EmailSender email,
    IOptions<AppOptions> app,
    ILogger<BusinessEventDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var e in events.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await hub.Clients.Group(InboxHub.GroupName(e.BusinessId)).SendAsync("businessEvent", new
                {
                    type = e.Type.ToString(),
                    e.Title,
                    e.Message,
                    e.Link,
                    e.ConversationId,
                    e.OrderId,
                }, stoppingToken);

                if (email.Enabled) await SendEmailAsync(e, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo notificar el evento {Type} del negocio {BusinessId}", e.Type, e.BusinessId);
            }
        }
    }

    private async Task SendEmailAsync(BusinessEvent e, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recipients = await db.NotificationEmails.AsNoTracking()
            .Where(n => n.BusinessId == e.BusinessId && n.IsActive)
            .Select(n => n.Email)
            .ToListAsync(ct);
        if (recipients.Count == 0) return;

        var businessName = await db.Businesses.Where(b => b.Id == e.BusinessId).Select(b => b.Name).FirstAsync(ct);
        var link = e.Link != null ? app.Value.Absolute(e.Link) : null;
        var subject = $"{e.Title} · {businessName}";
        var html = $"""
            <div style="font-family:Inter,Segoe UI,Arial,sans-serif;max-width:520px;margin:auto;padding:24px;color:#0f172a">
              <p style="font-size:13px;color:#16a34a;font-weight:600;margin:0 0 8px">InstaVende · {WebUtility.HtmlEncode(businessName)}</p>
              <h1 style="font-size:20px;margin:0 0 12px">{WebUtility.HtmlEncode(e.Title)}</h1>
              <p style="font-size:15px;line-height:1.5;margin:0 0 20px">{WebUtility.HtmlEncode(e.Message)}</p>
              {(link == null ? "" : $"<a href=\"{WebUtility.HtmlEncode(link)}\" style=\"display:inline-block;background:#16a34a;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none;font-weight:600\">Abrir en el panel</a>")}
              <p style="font-size:12px;color:#64748b;margin-top:28px">Recibes este aviso porque tu email está en Configuración → Notificaciones.</p>
            </div>
            """;
        var text = $"{e.Title}\n\n{e.Message}" + (link == null ? "" : $"\n\nAbrir en el panel: {link}");
        await email.SendAsync(recipients, subject, html, text, ct);
    }
}
