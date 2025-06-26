using Microsoft.AspNetCore.SignalR;

namespace ShopBoss.Web.Hubs;

public class StatusHub : Hub
{
    public async Task JoinWorkOrderGroup(string workOrderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"workorder-{workOrderId}");
    }

    public async Task LeaveWorkOrderGroup(string workOrderId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workorder-{workOrderId}");
    }

    public async Task JoinCncGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "cnc-station");
    }

    public async Task LeaveCncGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "cnc-station");
    }
}