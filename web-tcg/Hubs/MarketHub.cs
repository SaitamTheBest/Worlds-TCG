using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace web_tcg.Hubs;

[Authorize]
public sealed class MarketHub : Hub
{
    public Task WatchAuction(Guid id) => Groups.AddToGroupAsync(Context.ConnectionId, $"auction:{id}");
}
