using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace web_tcg.Hubs
{
    [Authorize]
    public class BalanceHub : Hub
    {
    }
}