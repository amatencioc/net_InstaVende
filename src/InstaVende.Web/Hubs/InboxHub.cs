using InstaVende.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InstaVende.Web.Hubs;

/// <summary>
/// Canal en tiempo real del panel. Cada conexión se une automáticamente (y solo) al grupo
/// de su propio negocio; el cliente no puede elegir el grupo.
/// Eventos emitidos: messageReceived, messageStatus, conversationUpdated (ver InboxNotifier).
/// </summary>
[Authorize]
public sealed class InboxHub(CurrentUserService currentUser) : Hub
{
    public static string GroupName(int businessId) => $"business_{businessId}";

    public override async Task OnConnectedAsync()
    {
        var businessId = await currentUser.GetBusinessIdAsync();
        if (businessId is null)
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(businessId.Value));
        await base.OnConnectedAsync();
    }
}
