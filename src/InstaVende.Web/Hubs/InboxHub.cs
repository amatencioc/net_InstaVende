using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InstaVende.Web.Hubs;

[Authorize]
public class InboxHub : Hub
{
    public async Task JoinBusinessGroup(string businessId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"business_{businessId}");

    public async Task LeaveBusinessGroup(string businessId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"business_{businessId}");
}
