using Microsoft.AspNetCore.SignalR;
using web_tcg.Hubs;

namespace web_tcg.Services
{
    public sealed class CardBalanceRealtimeService
    {
        private readonly IHubContext<CardBalanceHub> _hubContext;

        public CardBalanceRealtimeService(
            IHubContext<CardBalanceHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task PublishAsync(
            string userId,
            decimal cardBalance,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException(
                    "L'identifiant utilisateur est requis.",
                    nameof(userId));

            await _hubContext.Clients
                .User(userId)
                .SendAsync(
                    "CardBalanceUpdated",
                    new
                    {
                        balance = cardBalance
                    },
                    cancellationToken);
        }
    }
}