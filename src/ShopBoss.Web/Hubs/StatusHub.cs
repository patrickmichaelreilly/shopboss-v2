using Microsoft.AspNetCore.SignalR;

namespace ShopBoss.Web.Hubs;

public class StatusHub : Hub
{
    public async Task JoinWorkOrderGroup(string workOrderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"WorkOrder_{workOrderId}");
    }

    public async Task LeaveWorkOrderGroup(string workOrderId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"WorkOrder_{workOrderId}");
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task JoinCncGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "cnc-station");
    }

    public async Task LeaveCncGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "cnc-station");
    }

    public async Task JoinSortingGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "sorting-station");
    }

    public async Task LeaveSortingGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "sorting-station");
    }

    public async Task JoinAssemblyGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "assembly-station");
    }

    public async Task LeaveAssemblyGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "assembly-station");
    }

    public async Task JoinShippingGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "shipping-station");
    }

    public async Task LeaveShippingGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "shipping-station");
    }

    public async Task JoinAllStations()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-stations");
    }

    public async Task LeaveAllStations()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all-stations");
    }
}