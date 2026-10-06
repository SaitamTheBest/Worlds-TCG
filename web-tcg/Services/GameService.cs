using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;
using web_tcg.Hubs;
using web_tcg.Models.Game;

namespace web_tcg.Services
{
    public sealed class GameService
    {
        private readonly ApplicationDbContext _context;
        private readonly DeckValidationService _deckValidation;
        private readonly GameStateFactory _factory;
        private readonly GameStateRepository _repository;
        private readonly GameMatchLockProvider _locks;
        private readonly GameEngine _engine;
        private readonly GameStateProjector _projector;
        private readonly IHubContext<GameHub> _hub;
        private readonly CardBalanceRealtimeService _cardBalanceRealtime;
        private readonly GameRewardOptions _rewards;
        private readonly GameAiService _ai;

        public GameService(
            ApplicationDbContext context,
            DeckValidationService deckValidation,
            GameStateFactory factory,
            GameStateRepository repository,
            GameMatchLockProvider locks,
            GameEngine engine,
            GameStateProjector projector,
            IHubContext<GameHub> hub,
            CardBalanceRealtimeService cardBalanceRealtime,
            IOptions<GameRewardOptions> rewards,
            GameAiService ai)
        {
            _context = context;
            _deckValidation = deckValidation;
            _factory = factory;
            _repository = repository;
            _locks = locks;
            _engine = engine;
            _projector = projector;
            _hub = hub;
            _cardBalanceRealtime = cardBalanceRealtime;
            _rewards = rewards.Value;
            _ai = ai;
        }

        public async Task<Guid> CreateMatchAsync(
            string playerOneId,
            Guid playerOneDeckId,
            string playerTwoId,
            Guid playerTwoDeckId,
            CancellationToken cancellationToken = default)
        {
            if (playerOneId == playerTwoId)
            {
                throw new GameRuleException(
                    "Un joueur ne peut pas jouer contre lui-même.");
            }

            await _deckValidation.ValidateAsync(
                playerOneDeckId,
                playerOneId,
                cancellationToken);

            await _deckValidation.ValidateAsync(
                playerTwoDeckId,
                playerTwoId,
                cancellationToken);

            var matchId =
                Guid.NewGuid();

            var state =
                await _factory.CreateAsync(
                    matchId,
                    playerOneId,
                    playerOneDeckId,
                    playerTwoId,
                    playerTwoDeckId,
                    cancellationToken);

            var startLogs =
                await _engine.StartMatchAsync(
                    state,
                    cancellationToken);

            await RunAiTurnsIfNeededAsync(
                state,
                startLogs,
                cancellationToken);

            var match =
                new GameMatch
                {
                    Id =
                        matchId,

                    PlayerOneId =
                        playerOneId,

                    PlayerTwoId =
                        playerTwoId,

                    PlayerOneDeckId =
                        playerOneDeckId,

                    PlayerTwoDeckId =
                        playerTwoDeckId,

                    Status =
                        GameMatchStatus.Starting,

                    CreatedAt =
                        DateTime.UtcNow
                };

            _context.Set<GameMatch>().Add(
                match);

            await _context.SaveChangesAsync(
                cancellationToken);

            await _repository.SaveAsync(
                state,
                cancellationToken);

            await _repository.AddLogsAsync(
                matchId,
                startLogs,
                cancellationToken);

            return matchId;
        }

        public async Task EnsurePlayerInMatchAsync(
            Guid matchId,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var exists =
                await _context.Set<GameMatch>()
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.Id == matchId &&
                            (
                                x.PlayerOneId == userId ||
                                x.PlayerTwoId == userId
                            ),
                        cancellationToken);

            if (!exists)
            {
                throw new GameRuleException(
                    "Vous ne participez pas à cette partie.");
            }
        }

        public async Task SendStateToPlayerAsync(
            Guid matchId,
            string userId,
            CancellationToken cancellationToken = default)
        {
            await EnsurePlayerInMatchAsync(
                matchId,
                userId,
                cancellationToken);

            var state =
                await _repository.LoadAsync(
                    matchId,
                    cancellationToken)
                ?? throw new GameRuleException(
                    "Partie introuvable.");

            await _hub.Clients
                .User(userId)
                .SendAsync(
                    "GameStateUpdated",
                    _projector.Project(
                        state,
                        userId),
                    cancellationToken);
        }

        public Task PlayCardAsync(
            Guid matchId,
            string userId,
            Guid cardInstanceId,
            int? slot,
            CancellationToken cancellationToken = default,
            Guid? targetInstanceId = null,
            IReadOnlyList<GameHandCostSelection>? handCosts = null)
        {
            return ExecuteCommandAsync(
                matchId,
                userId,
                state =>
                    _engine.PlayCardAsync(
                        state,
                        userId,
                        cardInstanceId,
                        slot,
                        cancellationToken,
                        targetInstanceId, handCosts),
                cancellationToken, cardInstanceId, null, targetInstanceId, slot);
        }

        public Task UseAbilityAsync(
            Guid matchId,
            string userId,
            Guid sourceInstanceId,
            int abilityId,
            Guid? targetInstanceId,
            CancellationToken cancellationToken = default,
            IReadOnlyList<GameHandCostSelection>? handCosts = null)
        {
            return ExecuteCommandAsync(
                matchId,
                userId,
                state =>
                    _engine.UseAbilityAsync(
                        state,
                        userId,
                        sourceInstanceId,
                        abilityId,
                        targetInstanceId,
                        cancellationToken, handCosts),
                cancellationToken, sourceInstanceId, abilityId, targetInstanceId);
        }

        public async Task PreviewActionAsync(Guid matchId, string userId, Guid? sourceId, int? abilityId, Guid? targetId, int? slot)
        {
            await EnsurePlayerInMatchAsync(matchId, userId);
            var semaphore = _locks.Get(matchId);
            await semaphore.WaitAsync();
            try
            {
                var state = await _repository.LoadAsync(matchId) ?? throw new GameRuleException("Partie introuvable.");
                var preview = GameActionPreviewBuilder.Create(state, userId, sourceId, abilityId, targetId, slot);
                await _hub.Clients.Group($"game:{matchId}").SendAsync("GameActionPreviewUpdated", preview);
            }
            finally { semaphore.Release(); }
        }

        public Task EndTurnAsync(
            Guid matchId,
            string userId,
            CancellationToken cancellationToken = default)
        {
            return ExecuteCommandAsync(
                matchId,
                userId,
                state =>
                    _engine.EndTurnAsync(
                        state,
                        userId,
                        cancellationToken),
                cancellationToken);
        }

        public Task SurrenderAsync(
            Guid matchId,
            string userId,
            CancellationToken cancellationToken = default)
        {
            return ExecuteCommandAsync(
                matchId,
                userId,
                state =>
                    Task.FromResult(
                        _engine.Surrender(
                            state,
                            userId)),
                cancellationToken);
        }

        public async Task SendChatMessageAsync(
            Guid matchId,
            string userId,
            string message,
            CancellationToken cancellationToken = default)
        {
            await EnsurePlayerInMatchAsync(
                matchId,
                userId,
                cancellationToken);

            message =
                message.Trim();

            if (string.IsNullOrWhiteSpace(message) ||
                message.Length > 300)
            {
                throw new GameRuleException(
                    "Message invalide.");
            }

            var user =
                await _context.Users
                    .AsNoTracking()
                    .FirstAsync(
                        x =>
                            x.Id == userId,
                        cancellationToken);

            var chat =
                new GameChatMessage
                {
                    GameMatchId =
                        matchId,

                    UserId =
                        userId,

                    Message =
                        message,

                    CreatedAt =
                        DateTime.UtcNow
                };

            _context.Set<GameChatMessage>().Add(
                chat);

            await _context.SaveChangesAsync(
                cancellationToken);

            await _hub.Clients
                .Group($"game:{matchId}")
                .SendAsync(
                    "ChatMessageReceived",
                    new
                    {
                        userId,
                        username =
                            user.UserName,

                        message,

                        createdAt =
                            chat.CreatedAt
                    },
                    cancellationToken);
        }

        public async Task ProcessTimeoutsAsync(
            CancellationToken cancellationToken = default)
        {
            var matchIds =
                await _repository.GetActiveMatchIdsAsync(
                    cancellationToken);

            foreach (var matchId in matchIds)
            {
                var semaphore =
                    _locks.Get(matchId);

                await semaphore.WaitAsync(
                    cancellationToken);

                try
                {
                    var state =
                        await _repository.LoadAsync(
                            matchId,
                            cancellationToken);

                    if (state == null ||
                        state.Status != GameMatchStatus.InProgress)
                    {
                        continue;
                    }

                    var previousStatus =
                        state.Status;

                    _engine.ApplyTimeouts(
                        state,
                        DateTime.UtcNow);

                    if (state.Status == previousStatus)
                        continue;

                    state.Version++;

                    await _repository.SaveAsync(
                        state,
                        cancellationToken);

                    await CompleteMatchAsync(
                        state,
                        cancellationToken);

                    await BroadcastStateAsync(
                        state,
                        cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }

        private async Task RunAiTurnsIfNeededAsync(
            GameState state,
            List<string> logs,
            CancellationToken cancellationToken,
            bool progressive = false)
        {
            var safety = 0;

            while (state.Status == GameMatchStatus.InProgress &&
                   safety < 10)
            {
                safety++;

                var activePlayerId =
                    state.ActivePlayerId;

                var isBot =
                    await _context.Users
                        .AsNoTracking()
                        .Where(x => x.Id == activePlayerId)
                        .Select(x => x.IsBot)
                        .FirstOrDefaultAsync(
                            cancellationToken);

                if (!isBot)
                    break;

                if (progressive)
                {
                    await BroadcastStateAsync(state, cancellationToken);
                    await Task.Delay(650, cancellationToken);
                }
                var aiLogs =
                    await _ai.PlayTurnAsync(
                        state,
                        activePlayerId,
                        cancellationToken,
                        progressive ? async actionLogs =>
                        {
                            state.Version++;
                            state.LastActionAtUtc = DateTime.UtcNow;
                            await _repository.SaveAsync(state, cancellationToken);
                            await _repository.AddLogsAsync(state.MatchId, actionLogs, cancellationToken);
                            if(state.ActionReveals.Count>0) {
                                await _hub.Clients.Group($"game:{state.MatchId}").SendAsync("GameCardsRevealed",new { userId=activePlayerId,
                                    cards=state.ActionReveals.Select(c=>new { c.CardId,c.Name }).ToArray() },cancellationToken);
                                state.ActionReveals.Clear();
                            }
                            foreach (var log in actionLogs)
                                await _hub.Clients.Group($"game:{state.MatchId}").SendAsync("GameLogAdded", new { message = log, createdAt = DateTime.UtcNow }, cancellationToken);
                            await BroadcastStateAsync(state, cancellationToken);
                            if (state.Status == GameMatchStatus.InProgress) await Task.Delay(750, cancellationToken);
                        } : null,
                        progressive ? async preview =>
                        {
                            await _hub.Clients.Group($"game:{state.MatchId}").SendAsync("GameActionPreviewUpdated", preview, cancellationToken);
                            if (!preview.Confirmed) await Task.Delay(600, cancellationToken);
                        } : null);

                if (!progressive) logs.AddRange(aiLogs);
            }
        }

        private async Task ExecuteCommandAsync(
            Guid matchId,
            string userId,
            Func<GameState, Task<List<string>>> command,
            CancellationToken cancellationToken,
            Guid? sourceId = null, int? abilityId = null, Guid? targetId = null, int? slot = null)
        {
            var semaphore =
                _locks.Get(matchId);

            GameState? state = null;

            await semaphore.WaitAsync(
                cancellationToken);

            try
            {
                state =
                    await _repository.LoadAsync(
                        matchId,
                        cancellationToken)
                    ?? throw new GameRuleException(
                        "Partie introuvable.");

                await EnsurePlayerInMatchAsync(
                    matchId,
                    userId,
                    cancellationToken);

                var now =
                    DateTime.UtcNow;

                _engine.ApplyTimeouts(
                    state,
                    now);

                if (state.Status ==
                    GameMatchStatus.Finished)
                {
                    state.Version++;

                    await _repository.SaveAsync(
                        state,
                        cancellationToken);

                    await CompleteMatchAsync(
                        state,
                        cancellationToken);

                    throw new GameRuleException(
                        "La partie est terminée.");
                }

                _engine.ConsumeActiveClock(
                    state,
                    now);

                var source = sourceId.HasValue ? state.FindCard(sourceId.Value) : null;
                var ability = source?.Abilities.FirstOrDefault(a => a.Id == abilityId);
                var actionLabel = source == null ? null : ability != null ? $"{source.Name} · {ability.Name}" :
                    source.Zone == GameCardZone.Hand ? $"Joue {source.Name}" : source.Name;
                var isAttack = ability?.Effects.Any(e => e.Type == GameEffectType.Damage && e.Trigger ==
                    (ability.Type == AbilityType.Skill ? GameEffectTrigger.OnSkill : GameEffectTrigger.OnTalent)) == true;
                var logs =
                    await command(state);

                if (state.Status ==
                    GameMatchStatus.InProgress)
                {
                    state.LastActionAtUtc =
                        DateTime.UtcNow;
                }

                state.Version++;

                await _repository.SaveAsync(
                    state,
                    cancellationToken);

                if (logs.Count > 0)
                {
                    await _repository.AddLogsAsync(
                        matchId,
                        logs,
                        cancellationToken);

                    foreach (var log in logs)
                    {
                        await _hub.Clients
                            .Group($"game:{matchId}")
                            .SendAsync(
                                "GameLogAdded",
                                new
                                {
                                    message =
                                        log,

                                    createdAt =
                                        DateTime.UtcNow
                                },
                                cancellationToken);
                    }
                }

                if (state.ActionReveals.Count > 0) {
                    await _hub.Clients.Group($"game:{matchId}").SendAsync("GameCardsRevealed", new {
                        userId, cards = state.ActionReveals.Select(c=>new { c.CardId, c.Name }).ToArray()
                    }, cancellationToken);
                    state.ActionReveals.Clear();
                }

                if (actionLabel != null)
                {
                    await _hub.Clients.Group($"game:{matchId}").SendAsync("GameActionPreviewUpdated",
                        new GameActionPreviewDto { UserId = userId, Version = state.Version, SourceId = sourceId,
                            TargetId = targetId, Slot = slot, Label = actionLabel, IsAttack = isAttack, Confirmed = true }, cancellationToken);
                    await BroadcastStateAsync(state, cancellationToken);
                }
                await RunAiTurnsIfNeededAsync(state, logs, cancellationToken, progressive: true);

                if (state.Status ==
                    GameMatchStatus.Finished)
                {
                    await CompleteMatchAsync(
                        state,
                        cancellationToken);
                }
            }
            finally
            {
                semaphore.Release();
            }

            if (state != null)
            {
                await BroadcastStateAsync(
                    state,
                    cancellationToken);
            }
        }

        private async Task BroadcastStateAsync(
            GameState state,
            CancellationToken cancellationToken)
        {
            foreach (var playerId in new[]
                     {
                         state.PlayerOne.UserId,
                         state.PlayerTwo.UserId
                     })
            {
                await _hub.Clients
                    .User(playerId)
                    .SendAsync(
                        "GameStateUpdated",
                        _projector.Project(
                            state,
                            playerId),
                        cancellationToken);
            }
        }

        private async Task CompleteMatchAsync(
            GameState state,
            CancellationToken cancellationToken)
        {
            if (state.Status != GameMatchStatus.Finished ||
                string.IsNullOrWhiteSpace(state.WinnerUserId))
            {
                return;
            }

            await using var rewardTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await CollectionTransactionLock.AcquireAsync(_context, cancellationToken);

            var match =
                await _context.Set<GameMatch>()
                    .FirstAsync(
                        x =>
                            x.Id ==
                            state.MatchId,
                        cancellationToken);

            await _context.Entry(match).ReloadAsync(cancellationToken);
            if (match.RewardsGranted)
                return;

            var winner =
                await _context.Users
                    .FirstAsync(
                        x =>
                            x.Id ==
                            state.WinnerUserId,
                        cancellationToken);

            await _context.Entry(winner).ReloadAsync(cancellationToken);
            winner.XP +=
                _rewards.WinXp;

            winner.CardBalance +=
                _rewards.WinCardBalance;

            match.RewardsGranted =
                true;

            await _context.SaveChangesAsync(
                cancellationToken);

            state.RewardXp = _rewards.WinXp;
            state.RewardCardBalance = _rewards.WinCardBalance;
            await _repository.SaveAsync(state, cancellationToken);
            await rewardTransaction.CommitAsync(cancellationToken);

            await _cardBalanceRealtime.PublishAsync(
                winner.Id,
                winner.CardBalance,
                cancellationToken);

            await _hub.Clients
                .Group($"game:{state.MatchId}")
                .SendAsync(
                    "GameFinished",
                    new
                    {
                        winnerUserId =
                            winner.Id,

                        xpReward =
                            _rewards.WinXp,

                        cardBalanceReward =
                            _rewards.WinCardBalance,

                        reason =
                            state.EndReason?.ToString()
                    },
                    cancellationToken);
        }
    }
}
