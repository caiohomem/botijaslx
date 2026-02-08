using Microsoft.AspNetCore.SignalR;

namespace Botijas.Infrastructure.Hubs;

public class PrintHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "PrintGateway");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "PrintGateway");
        await base.OnDisconnectedAsync(exception);
    }
}
