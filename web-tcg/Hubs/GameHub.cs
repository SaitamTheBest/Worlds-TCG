using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using web_tcg.Game.Engine;
using web_tcg.Services;

namespace web_tcg.Hubs
{
    [Authorize]
    public sealed class GameHub : Hub
    {
        private readonly GameService _gameService;

        public GameHub(
            GameService gameService)
        {
            _gameService =
                gameService;
        }

        private string UserId =>
            Context.User?.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? throw new HubException(
                "Utilisateur non authentifié.");

        public async Task JoinGame(
            Guid matchId)
        {
            await ExecuteAsync(
                async () =>
                {
                    await _gameService
                        .EnsurePlayerInMatchAsync(
                            matchId,
                            UserId);

                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        $"game:{matchId}");

                    await _gameService
                        .SendStateToPlayerAsync(
                            matchId,
                            UserId);
                });
        }

        public Task PlayCard(
            Guid matchId,
            Guid cardInstanceId,
            int? slot)
        {
            return ExecuteAsync(
                () =>
                    _gameService.PlayCardAsync(
                        matchId,
                        UserId,
                        cardInstanceId,
                        slot));
        }

        public Task PlayCardWithTarget(Guid matchId, Guid cardInstanceId, int? slot, Guid? targetInstanceId)
        {
            return ExecuteAsync(() => _gameService.PlayCardAsync(matchId, UserId, cardInstanceId, slot, targetInstanceId: targetInstanceId));
        }

        public Task Block(
            Guid matchId,
            Guid sourceInstanceId)
        {
            return ExecuteAsync(
                () =>
                    _gameService.BlockAsync(
                        matchId,
                        UserId,
                        sourceInstanceId));
        }

        public Task UseAbility(
            Guid matchId,
            Guid sourceInstanceId,
            int abilityId,
            Guid? targetInstanceId)
        {
            return ExecuteAsync(
                () =>
                    _gameService.UseAbilityAsync(
                        matchId,
                        UserId,
                        sourceInstanceId,
                        abilityId,
                        targetInstanceId));
        }

        public Task EndTurn(
            Guid matchId)
        {
            return ExecuteAsync(
                () =>
                    _gameService.EndTurnAsync(
                        matchId,
                        UserId));
        }

        public Task Surrender(
            Guid matchId)
        {
            return ExecuteAsync(
                () =>
                    _gameService.SurrenderAsync(
                        matchId,
                        UserId));
        }

        public Task SendChatMessage(
            Guid matchId,
            string message)
        {
            return ExecuteAsync(
                () =>
                    _gameService.SendChatMessageAsync(
                        matchId,
                        UserId,
                        message));
        }

        private static async Task ExecuteAsync(
            Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (GameRuleException ex)
            {
                throw new HubException(
                    ex.Message);
            }
        }
    }
}
