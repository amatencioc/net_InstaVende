using InstaVende.Core.Enums;

namespace InstaVende.Core.Interfaces;

/// <summary>Resultado de un envío por canal. <see cref="ExternalMessageId"/> es el id asignado por el proveedor.</summary>
public sealed record SendResult(bool Success, string? ExternalMessageId = null, string? Error = null)
{
    public static SendResult Ok(string? externalMessageId = null) => new(true, externalMessageId);
    public static SendResult Fail(string error) => new(false, null, error);
}

public interface IChannelMessageSender
{
    ChannelType Channel { get; }
    Task<SendResult> SendTextAsync(int businessId, string recipientExternalId, string text, CancellationToken ct = default);
}
