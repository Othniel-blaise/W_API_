using Microsoft.AspNetCore.SignalR;

namespace W_API.Api.Hubs;

public class LogHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ReceiveLog", "INF",
            "Terminal connecté — en attente d'un traitement.", DateTime.Now.ToString("HH:mm:ss"));
        await base.OnConnectedAsync();
    }
}
