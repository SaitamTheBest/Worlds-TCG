using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using web_tcg.Game.Engine;
using web_tcg.Services;

namespace web_tcg.Hubs
{
    [Authorize]
    public sealed class LobbyHub : Hub
    {
        private readonly MatchmakingService _matchmaking;
        private readonly DeckValidationService _deckValidation;
        private readonly GameService _gameService;

        public LobbyHub(
            MatchmakingService matchmaking,
            DeckValidationService deckValidation,
            GameService gameService)
        {
            _matchmaking = matchmaking;
            _deckValidation = deckValidation;
            _gameService = gameService;
        }

        private string UserId =>
            Context.User?.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? throw new HubException(
                "Utilisateur non authentifié.");

        public async Task JoinQueue(
            Guid deckId)
        {
            MatchmakingService.QueueEntry? opponent = null;
            try
            {
                await _deckValidation.ValidateAsync(
                    deckId,
                    UserId);

                opponent =
                    await _matchmaking.TryEnqueueAndMatchAsync(
                        UserId,
                        deckId,
                        connectionId: Context.ConnectionId);

                if (opponent == null)
                {
                    await Clients.Caller.SendAsync(
                        "QueueStatusUpdated",
                        "waiting");

                    return;
                }

                var matchId =
                    await _gameService.CreateMatchAsync(
                        opponent.UserId,
                        opponent.DeckId,
                        UserId,
                        deckId);

                await Clients.User(opponent.UserId)
                    .SendAsync(
                        "MatchFound",
                        matchId);

                await Clients.Caller.SendAsync(
                    "MatchFound",
                    matchId);
            }
            catch (GameRuleException ex)
            {
                if (opponent != null)
                    await Clients.User(opponent.UserId).SendAsync("QueueError", ex.Message);
                throw new HubException(
                    ex.Message);
            }
            catch (Exception ex)
            {
                Context.GetHttpContext()?.RequestServices.GetRequiredService<ILogger<LobbyHub>>()
                    .LogError(ex, "Impossible de créer une partie PVP pour {UserId}", UserId);
                const string message = "Impossible de créer la partie PVP. Veuillez réessayer.";
                if (opponent != null)
                    await Clients.User(opponent.UserId).SendAsync("QueueError", message);
                throw new HubException(message);
            }
        }

        public Task LeaveQueue()
        {
            _matchmaking.Leave(
                UserId, Context.ConnectionId);

            return Task.CompletedTask;
        }

        public override Task OnDisconnectedAsync(
            Exception? exception)
        {
            _matchmaking.Leave(
                UserId, Context.ConnectionId);

            return base.OnDisconnectedAsync(exception);
        }
    }
}
