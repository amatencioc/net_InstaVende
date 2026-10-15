using System.Linq.Expressions;
using InstaVende.Core.Domain;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using InstaVende.Web.Features.Shared;
using InstaVende.Web.Services;
using InstaVende.Web.Services.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.Inbox;

/// <summary>
/// Bandeja de mensajes: conversaciones de todos los canales, historial, respuestas manuales
/// y control humano/bot. Los cambios se notifican en tiempo real por <see cref="InboxNotifier"/>.
/// </summary>
[Route("api/conversations")]
public sealed class InboxController(
    AppDbContext db,
    MessageDispatcher dispatcher,
    InboxNotifier notifier,
    CurrentUserService currentUser) : TenantController
{
    private const int MaxSearchLength = 100;
    private const int DefaultMessagePageSize = 50;

    // ── Listado ──────────────────────────────────────────────────────────────

    /// <summary>Conversaciones ordenadas por último mensaje (más recientes primero).</summary>
    /// <param name="status">Categoría: All, Unattended, BotActive, WaitingHuman, HumanActive o Resolved.</param>
    /// <param name="channel">Canal de origen.</param>
    /// <param name="search">Nombre o teléfono del contacto, o texto del último mensaje.</param>
    /// <param name="labelId">Etiqueta asignada.</param>
    /// <param name="page">Página (desde 1).</param>
    /// <param name="pageSize">Tamaño de página (1–100, por defecto 30).</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<InboxConversationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<InboxConversationDto>>> List(
        [FromQuery] InboxStatusFilter status = InboxStatusFilter.All, [FromQuery] ChannelType? channel = null,
        [FromQuery] string? search = null, [FromQuery] int? labelId = null, [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null, CancellationToken ct = default)
    {
        var query = ApplyStatus(Filtered(channel, labelId), status);

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            if (term.Length > MaxSearchLength) term = term[..MaxSearchLength];
            query = query.Where(c =>
                (c.Contact.Name != null && c.Contact.Name.Contains(term)) ||
                (c.Contact.Phone != null && c.Contact.Phone.Contains(term)) ||
                c.Contact.ExternalId.Contains(term) ||
                (c.LastMessagePreview != null && c.LastMessagePreview.Contains(term)));
        }

        return await query
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Select(ConversationProjection)
            .ToPagedResultAsync(PageRequest.From(page, pageSize, defaultSize: 30), ct);
    }

    /// <summary>Totales por categoría (respetan los filtros de canal y etiqueta).</summary>
    [HttpGet("counts")]
    [ProducesResponseType<InboxCountsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InboxCountsDto>> Counts([FromQuery] ChannelType? channel, [FromQuery] int? labelId, CancellationToken ct)
    {
        var counts = await Filtered(channel, labelId)
            .GroupBy(c => c.BusinessId)
            .Select(g => new InboxCountsDto(
                g.Count(),
                g.Count(c => (c.Status == ConversationStatus.WaitingHuman || c.Status == ConversationStatus.HumanActive) && c.UnreadCount > 0),
                g.Count(c => c.Status == ConversationStatus.BotActive),
                g.Count(c => c.Status == ConversationStatus.WaitingHuman),
                g.Count(c => c.Status == ConversationStatus.HumanActive),
                g.Count(c => c.Status == ConversationStatus.Resolved)))
            .FirstOrDefaultAsync(ct);

        return counts ?? new InboxCountsDto(0, 0, 0, 0, 0, 0);
    }

    // ── Detalle ──────────────────────────────────────────────────────────────

    /// <summary>Detalle de la conversación con la ficha del contacto y sus últimos pedidos.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<InboxConversationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InboxConversationDetailDto>> Get(int id, CancellationToken ct)
    {
        var bid = BusinessId;
        var row = await db.Conversations.AsNoTracking()
            .Where(c => c.Id == id && c.BusinessId == bid)
            .Select(c => new
            {
                c.Id, c.ChannelType, c.Status, c.UnreadCount, c.CreatedAt, c.ResolvedAt, c.LastMessageAt,
                Label = c.LabelId == null ? null : new InboxLabelRefDto(c.Label!.Id, c.Label.Name, c.Label.Color),
                Agent = c.AssignedAgentId == null ? null
                    : new InboxAgentDto(c.AssignedAgentId, c.AssignedAgent!.FirstName, c.AssignedAgent.LastName),
                Contact = new
                {
                    c.Contact.Id, c.Contact.Name, c.Contact.Phone, c.Contact.ExternalId, c.Contact.AvatarUrl,
                    c.Contact.ChannelType, c.Contact.FirstSeenAt, c.Contact.LastSeenAt,
                    c.Contact.TotalPurchases, c.Contact.TotalSpent, c.Contact.LastPurchaseAt,
                },
                Orders = c.Contact.Orders
                    .Where(o => o.BusinessId == bid && o.Status != OrderStatus.Cart)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)
                    .Select(o => new { o.Id, o.OrderNumber, o.Status, o.SubStatus, o.Total, o.CreatedAt })
                    .ToList(),
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (row is null) return NotFoundProblem("La conversación no existe o fue eliminada.");

        var orders = row.Orders
            .Select(o => new InboxContactOrderDto(o.Id, o.OrderNumber, o.Status, o.SubStatus,
                OrderWorkflow.Label(o.SubStatus), o.Total, o.CreatedAt))
            .ToList();

        var k = row.Contact;
        return new InboxConversationDetailDto(
            row.Id, row.ChannelType, row.Status, row.UnreadCount, row.CreatedAt, row.ResolvedAt, row.LastMessageAt,
            row.Label, row.Agent,
            new InboxContactDetailDto(k.Id, k.Name, k.Phone, k.ExternalId, k.AvatarUrl, k.ChannelType, k.FirstSeenAt,
                k.LastSeenAt, k.TotalPurchases, k.TotalSpent, k.LastPurchaseAt, orders));
    }

    // ── Mensajes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Página de mensajes en orden cronológico ascendente. Sin <paramref name="before"/> devuelve los más recientes;
    /// con <paramref name="before"/> devuelve los anteriores a ese mensaje.
    /// </summary>
    /// <param name="id">Conversación.</param>
    /// <param name="before">Id del mensaje más antiguo ya cargado.</param>
    /// <param name="limit">Cantidad máxima (1–100, por defecto 50).</param>
    [HttpGet("{id:int}/messages")]
    [ProducesResponseType<InboxMessagePageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InboxMessagePageDto>> Messages(int id, [FromQuery] int? before, [FromQuery] int? limit, CancellationToken ct)
    {
        var bid = BusinessId;
        if (!await db.Conversations.AnyAsync(c => c.Id == id && c.BusinessId == bid, ct))
            return NotFoundProblem("La conversación no existe o fue eliminada.");

        var take = Math.Clamp(limit ?? DefaultMessagePageSize, 1, 100);
        var query = db.Messages.AsNoTracking().Where(m => m.ConversationId == id);

        if (before is int beforeId)
        {
            var cursor = await db.Messages.AsNoTracking()
                .Where(m => m.Id == beforeId && m.ConversationId == id)
                .Select(m => (DateTime?)m.SentAt)
                .FirstOrDefaultAsync(ct);
            if (cursor is not DateTime at)
                return BadRequestProblem("El mensaje de referencia no pertenece a esta conversación.");
            query = query.Where(m => m.SentAt < at || (m.SentAt == at && m.Id < beforeId));
        }

        var items = await query
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Take(take + 1)
            .Select(MessageProjection)
            .ToListAsync(ct);

        var hasMore = items.Count > take;
        if (hasMore) items.RemoveAt(items.Count - 1);
        items.Reverse();
        return new InboxMessagePageDto(items, hasMore);
    }

    /// <summary>Marca como leídos los mensajes entrantes y reinicia el contador de no leídos.</summary>
    [HttpPost("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        var bid = BusinessId;
        var conv = await db.Conversations.AsNoTracking()
            .Where(c => c.Id == id && c.BusinessId == bid)
            .Select(c => new { c.Status, c.UnreadCount })
            .FirstOrDefaultAsync(ct);
        if (conv is null) return NotFoundProblem("La conversación no existe o fue eliminada.");

        await db.Messages
            .Where(m => m.ConversationId == id && m.Direction == MessageDirection.Inbound && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true), ct);

        if (conv.UnreadCount > 0)
        {
            await db.Conversations
                .Where(c => c.Id == id && c.BusinessId == bid)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UnreadCount, 0), ct);
            // Sincroniza contadores en las demás sesiones del negocio
            await notifier.ConversationUpdatedAsync(bid, id, conv.Status, ct);
        }
        return NoContent();
    }

    /// <summary>
    /// Envía un mensaje manual. Si el bot atendía (o se esperaba a un humano) el usuario toma el control.
    /// El resultado incluye el estado de entrega: <c>Failed</c> si el canal rechazó el envío.
    /// </summary>
    [HttpPost("{id:int}/messages")]
    [ProducesResponseType<InboxMessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InboxMessageDto>> Send(int id, InboxSendMessageRequest request, CancellationToken ct)
    {
        var text = request.Text.Trim();
        if (text.Length == 0) return BadRequestProblem("Escribe un mensaje.");

        var bid = BusinessId;
        var conv = await db.Conversations.AsNoTracking()
            .Where(c => c.Id == id && c.BusinessId == bid)
            .Select(c => new { c.Status, c.AssignedAgentId })
            .FirstOrDefaultAsync(ct);
        if (conv is null) return NotFoundProblem("La conversación no existe o fue eliminada.");
        if (conv.Status == ConversationStatus.Resolved)
            return ConflictProblem("La conversación está resuelta. Reábrela para responder.");

        // Se toma el control antes de enviar para que el bot no responda en paralelo
        var tookControl = false;
        if (conv.Status != ConversationStatus.HumanActive || conv.AssignedAgentId is null)
        {
            var userId = currentUser.GetUserId();
            var now = DateTime.UtcNow;
            tookControl = await db.Conversations
                .Where(c => c.Id == id && c.BusinessId == bid && c.Status == conv.Status)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, ConversationStatus.HumanActive)
                    .SetProperty(c => c.AssignedAgentId, userId)
                    .SetProperty(c => c.UpdatedAt, now), ct) > 0;
        }

        // El envío no se cancela si el cliente se desconecta: el estado de entrega debe quedar registrado
        var message = await dispatcher.SendAsync(bid, id, text, sentByBot: false, CancellationToken.None);

        if (tookControl)
            await notifier.ConversationUpdatedAsync(bid, id, ConversationStatus.HumanActive, CancellationToken.None);

        return ToDto(message);
    }

    /// <summary>Reintenta un mensaje saliente que no se entregó (se reemplaza por un envío nuevo).</summary>
    [HttpPost("{id:int}/messages/{messageId:int}/retry")]
    [ProducesResponseType<InboxMessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InboxMessageDto>> Retry(int id, int messageId, CancellationToken ct)
    {
        var bid = BusinessId;
        var status = await db.Conversations.AsNoTracking()
            .Where(c => c.Id == id && c.BusinessId == bid)
            .Select(c => (ConversationStatus?)c.Status)
            .FirstOrDefaultAsync(ct);
        if (status is null) return NotFoundProblem("La conversación no existe o fue eliminada.");
        if (status == ConversationStatus.Resolved)
            return ConflictProblem("La conversación está resuelta. Reábrela para responder.");

        var failed = await db.Messages.AsNoTracking()
            .Where(m => m.Id == messageId && m.ConversationId == id)
            .Select(m => new { m.Direction, m.DeliveryStatus, m.Content, m.SentByBot })
            .FirstOrDefaultAsync(ct);
        if (failed is null) return NotFoundProblem("El mensaje no existe.");
        if (failed.Direction != MessageDirection.Outbound || failed.DeliveryStatus != MessageDeliveryStatus.Failed)
            return ConflictProblem("Solo se pueden reintentar mensajes que no se entregaron.");

        // El borrado condicional evita reintentos duplicados concurrentes
        var removed = await db.Messages
            .Where(m => m.Id == messageId && m.ConversationId == id && m.DeliveryStatus == MessageDeliveryStatus.Failed)
            .ExecuteDeleteAsync(ct);
        if (removed == 0) return ConflictProblem("El mensaje ya se está reintentando.");

        var message = await dispatcher.SendAsync(bid, id, failed.Content, failed.SentByBot, CancellationToken.None);
        return ToDto(message);
    }

    // ── Estado y etiqueta ────────────────────────────────────────────────────

    /// <summary>
    /// Cambia el estado: tomar control (HumanActive), devolver al bot (BotActive), resolver (Resolved)
    /// o reabrir (BotActive/HumanActive desde Resolved).
    /// </summary>
    [HttpPost("{id:int}/status")]
    [ProducesResponseType<InboxConversationStateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InboxConversationStateDto>> ChangeStatus(int id, InboxChangeStatusRequest request, CancellationToken ct)
    {
        var target = request.Status;
        if (!Enum.IsDefined(target))
            return BadRequestProblem("Estado no válido.");
        if (target == ConversationStatus.WaitingHuman)
            return BadRequestProblem("El estado «Esperando humano» solo lo asigna el vendedor IA.");

        var bid = BusinessId;
        var conv = await db.Conversations.AsNoTracking()
            .Where(c => c.Id == id && c.BusinessId == bid)
            .Select(c => new { c.Status, c.AssignedAgentId, c.ContactId, c.ChannelType })
            .FirstOrDefaultAsync(ct);
        if (conv is null) return NotFoundProblem("La conversación no existe o fue eliminada.");

        var userId = currentUser.GetUserId();
        var unchanged = conv.Status == target && (target != ConversationStatus.HumanActive || conv.AssignedAgentId == userId);
        if (unchanged) return await LoadStateAsync(id, ct);

        // Al reabrir no puede quedar otra conversación abierta del mismo contacto y canal
        if (conv.Status == ConversationStatus.Resolved)
        {
            var otherOpen = await db.Conversations.AnyAsync(c =>
                c.BusinessId == bid && c.ContactId == conv.ContactId && c.ChannelType == conv.ChannelType &&
                c.Id != id && c.Status != ConversationStatus.Resolved, ct);
            if (otherOpen)
                return ConflictProblem("Este contacto ya tiene otra conversación abierta en el mismo canal.");
        }

        var agentId = target switch
        {
            ConversationStatus.HumanActive => userId,
            ConversationStatus.Resolved => conv.AssignedAgentId,
            _ => null,
        };
        var now = DateTime.UtcNow;
        DateTime? resolvedAt = target == ConversationStatus.Resolved ? now : null;

        var updated = await db.Conversations
            .Where(c => c.Id == id && c.BusinessId == bid && c.Status == conv.Status)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, target)
                .SetProperty(c => c.AssignedAgentId, agentId)
                .SetProperty(c => c.ResolvedAt, resolvedAt)
                .SetProperty(c => c.UpdatedAt, now), ct);
        if (updated == 0)
            return ConflictProblem("La conversación cambió mientras tanto. Vuelve a intentarlo.");

        await notifier.ConversationUpdatedAsync(bid, id, target, CancellationToken.None);
        return await LoadStateAsync(id, ct);
    }

    /// <summary>Asigna o quita (<c>labelId = null</c>) la etiqueta de la conversación.</summary>
    [HttpPut("{id:int}/label")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetLabel(int id, InboxSetLabelRequest request, CancellationToken ct)
    {
        var bid = BusinessId;
        if (request.LabelId is int labelId &&
            !await db.ConversationLabels.AnyAsync(l => l.Id == labelId && l.BusinessId == bid, ct))
            return BadRequestProblem("La etiqueta no existe.");

        var updated = await db.Conversations
            .Where(c => c.Id == id && c.BusinessId == bid)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LabelId, request.LabelId), ct);
        return updated == 0 ? NotFoundProblem("La conversación no existe o fue eliminada.") : NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private IQueryable<Conversation> Filtered(ChannelType? channel, int? labelId)
    {
        var bid = BusinessId;
        var query = db.Conversations.AsNoTracking().Where(c => c.BusinessId == bid);
        if (channel is ChannelType ch) query = query.Where(c => c.ChannelType == ch);
        if (labelId is int lid) query = query.Where(c => c.LabelId == lid);
        return query;
    }

    private static IQueryable<Conversation> ApplyStatus(IQueryable<Conversation> query, InboxStatusFilter status) => status switch
    {
        InboxStatusFilter.Unattended => query.Where(c =>
            (c.Status == ConversationStatus.WaitingHuman || c.Status == ConversationStatus.HumanActive) && c.UnreadCount > 0),
        InboxStatusFilter.BotActive => query.Where(c => c.Status == ConversationStatus.BotActive),
        InboxStatusFilter.WaitingHuman => query.Where(c => c.Status == ConversationStatus.WaitingHuman),
        InboxStatusFilter.HumanActive => query.Where(c => c.Status == ConversationStatus.HumanActive),
        InboxStatusFilter.Resolved => query.Where(c => c.Status == ConversationStatus.Resolved),
        _ => query,
    };

    private async Task<ActionResult<InboxConversationStateDto>> LoadStateAsync(int id, CancellationToken ct)
    {
        var bid = BusinessId;
        var state = await db.Conversations.AsNoTracking()
            .Where(c => c.Id == id && c.BusinessId == bid)
            .Select(c => new InboxConversationStateDto(c.Id, c.Status,
                c.AssignedAgentId == null ? null
                    : new InboxAgentDto(c.AssignedAgentId, c.AssignedAgent!.FirstName, c.AssignedAgent.LastName),
                c.ResolvedAt))
            .FirstOrDefaultAsync(ct);
        return state is null ? NotFoundProblem("La conversación no existe o fue eliminada.") : state;
    }

    private static readonly Expression<Func<Conversation, InboxConversationDto>> ConversationProjection = c =>
        new InboxConversationDto(
            c.Id,
            new InboxContactSummaryDto(c.Contact.Id, c.Contact.Name, c.Contact.Phone, c.Contact.ExternalId, c.Contact.AvatarUrl),
            c.ChannelType,
            c.Status,
            c.UnreadCount,
            c.LastMessagePreview,
            c.LastMessageAt,
            c.LabelId == null ? null : new InboxLabelRefDto(c.Label!.Id, c.Label.Name, c.Label.Color),
            c.AssignedAgentId == null ? null
                : new InboxAgentDto(c.AssignedAgentId, c.AssignedAgent!.FirstName, c.AssignedAgent.LastName));

    private static readonly Expression<Func<Message, InboxMessageDto>> MessageProjection = m =>
        new InboxMessageDto(m.Id, m.ConversationId, m.Direction, m.Content, m.SentByBot, m.DeliveryStatus, m.SentAt,
            m.MediaUrl, m.MediaType);

    private static InboxMessageDto ToDto(Message m)
        => new(m.Id, m.ConversationId, m.Direction, m.Content, m.SentByBot, m.DeliveryStatus, m.SentAt, m.MediaUrl, m.MediaType);
}
