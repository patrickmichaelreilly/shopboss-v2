using Microsoft.AspNetCore.SignalR;

namespace ShopBoss.Web.Hubs;

public class ImportProgressHub : Hub
{
    public async Task JoinImportGroup(string importId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"import-{importId}");
    }

    public async Task LeaveImportGroup(string importId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"import-{importId}");
    }
}