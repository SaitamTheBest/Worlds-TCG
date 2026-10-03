using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace web_tcg.Hubs
{
    [Authorize]
    public sealed class NotificationHub : Hub
    {
    }
}