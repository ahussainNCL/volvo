using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Nordlink.Cloud;

[Authorize]
public sealed class CloudHub(Relay relay) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var id = Context.User?.FindFirst("sid")?.Value ?? "";
        if (!relay.Active(id)) { Context.Abort(); return; }
        await Groups.AddToGroupAsync(Context.ConnectionId, id); await base.OnConnectedAsync();
    }
}
