using InstaVende.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InstaVende.Web.Hubs;

[Authorize]
public class InboxHub : Hub
{
    private readonly CurrentUserService _cu;

    public InboxHub(CurrentUserService cu) => _cu = cu;

    /// <summary>
    /// Joins the SignalR group for the caller's own business only.
    /// The businessId parameter is ignored — always uses the authenticated user's business.
    /// </summary>
    public async Task JoinBusinessGroup(string businessId)
    {
        var ownId = await _cu.GetBusinessIdAsync();
        if (ownId == null) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"business_{ownId}");
    }

    public async Task LeaveBusinessGroup(string businessId)
    {
        var ownId = await _cu.GetBusinessIdAsync();
        if (ownId == null) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"business_{ownId}");
    }
}
