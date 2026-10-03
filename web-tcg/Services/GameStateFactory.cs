using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;

namespace web_tcg.Services
{
    public sealed class GameStateFactory
    {
        private readonly ApplicationDbContext _context;

        public GameStateFactory(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<GameState> CreateAsync(
            Guid matchId,
            string playerOneId,
            Guid playerOneDeckId,
            string playerTwoId,
            Guid playerTwoDeckId,
            CancellationToken cancellationToken = default)
        {
            var playerOne =
                await BuildPlayerAsync(
                    playerOneId,
                    playerOneDeckId,
                    cancellationToken);

            var playerTwo =
                await BuildPlayerAsync(
                    playerTwoId,
                    playerTwoDeckId,
                    cancellationToken);

            var now =
                DateTime.UtcNow;

            return new GameState
            {
                MatchId = matchId,
                Version = 0,
                Status = GameMatchStatus.Starting,
                TurnNumber = 0,
                TurnStartedAtUtc = now,
                ClockAnchorUtc = now,
                LastActionAtUtc = now,
                PlayerOne = playerOne,
                PlayerTwo = playerTwo
            };
        }

        private async Task<GamePlayerState> BuildPlayerAsync(
            string userId,
            Guid deckId,
            CancellationToken cancellationToken)
        {
            var user =
                await _context.Users
                    .AsNoTracking()
                    .FirstAsync(
                        x => x.Id == userId,
                        cancellationToken);

            var deck =
                await _context.Set<Deck>()
                    .AsNoTracking()
                    .Include(x => x.Cards)
                        .ThenInclude(x => x.Card)
                            .ThenInclude(x => x.Abilities)
                                .ThenInclude(x => x.Effects)
                    .Include(x => x.Cards)
                        .ThenInclude(x => x.Card)
                            .ThenInclude(x => x.Abilities)
                                .ThenInclude(x => x.Conditions)
                    .Include(x => x.LeaderCard)
                        .ThenInclude(x => x.Abilities)
                            .ThenInclude(x => x.Effects)
                    .Include(x => x.LeaderCard)
                        .ThenInclude(x => x.Abilities)
                            .ThenInclude(x => x.Conditions)
                    .FirstAsync(
                        x => x.Id == deckId,
                        cancellationToken);

            var leaderGameplayId =
                deck.LeaderCard.BaseCardId ??
                deck.LeaderCard.Id;

            var leaderGameplayCard =
                await _context.Cards
                    .AsNoTracking()
                    .Include(x => x.Abilities)
                        .ThenInclude(x => x.Effects)
                    .Include(x => x.Abilities)
                        .ThenInclude(x => x.Conditions)
                    .FirstAsync(
                        x => x.Id == leaderGameplayId,
                        cancellationToken);

            var leaderStat =
                await _context.LeaderCards
                    .AsNoTracking()
                    .FirstAsync(
                        x => x.CardId == leaderGameplayId,
                        cancellationToken);

            var leader =
                BuildCardState(
                    deck.LeaderCard,
                    leaderGameplayCard,
                    userId,
                    leaderStat.HealthPoints);

            leader.Zone =
                GameCardZone.Leader;

            var player =
                new GamePlayerState
                {
                    UserId = userId,
                    DisplayName = user.UserName ?? userId,
                    AvatarUrl = user.AvatarUrl,
                    Money = 0,
                    Leader = leader
                };

            foreach (var deckCard in deck.Cards)
            {
                var gameplayId =
                    deckCard.Card.BaseCardId ??
                    deckCard.Card.Id;

                var gameplayCard =
                    await _context.Cards
                        .AsNoTracking()
                        .Include(x => x.Abilities)
                            .ThenInclude(x => x.Effects)
                        .Include(x => x.Abilities)
                            .ThenInclude(x => x.Conditions)
                        .FirstAsync(
                            x => x.Id == gameplayId,
                            cancellationToken);

                var characterStat =
                    gameplayCard.Type == CardType.Character
                        ? await _context.CharacterCards
                            .AsNoTracking()
                            .FirstOrDefaultAsync(
                                x => x.CardId == gameplayId,
                                cancellationToken)
                        : null;

                for (var i = 0; i < deckCard.Quantity; i++)
                {
                    var card =
                        BuildCardState(
                            deckCard.Card,
                            gameplayCard,
                            userId,
                            characterStat?.HealthPoints);

                    card.Zone =
                        GameCardZone.Deck;

                    player.Deck.Add(
                        card);
                }
            }

            return player;
        }

        private static GameCardState BuildCardState(
            Card printedCard,
            Card gameplayCard,
            string userId,
            int? healthPoints)
        {
            var card =
                new GameCardState
                {
                    InstanceId = Guid.NewGuid(),
                    CardId = printedCard.Id,
                    GameplayCardId = gameplayCard.Id,
                    RegionId = gameplayCard.RegionId,
                    CountryId = gameplayCard.CountryId,
                    OwnerUserId = userId,
                    ControllerUserId = userId,
                    Name = printedCard.Name,
                    Type = gameplayCard.Type,
                    Rarity = printedCard.Rarity,
                    ImageUrl = printedCard.ImageUrl,
                    UsageCost = gameplayCard.UsageCost,
                    MaxHealth = healthPoints,
                    BaseHealth = healthPoints,
                    CurrentHealth = healthPoints
                };

            foreach (var ability in gameplayCard.Abilities
                         .OrderBy(x => x.DisplayOrder))
            {
                card.Abilities.Add(
                    new GameAbilityState
                    {
                        Id = ability.Id,
                        Type = ability.Type,
                        Name = ability.Name,
                        Description = ability.Description,
                        UsageCost = ability.UsageCost,
                        CooldownTurns = ability.CooldownTurns,
                        Effects = ability.Effects
                            .OrderBy(x => x.DisplayOrder)
                            .Select(x => new GameEffectDefinition
                            {
                                Type = x.Type,
                                Trigger = x.Trigger,
                                Target = x.Target,
                                Value = x.Value,
                                DurationTurns = x.DurationTurns,
                                Parameters = ParseParameters(x.ParametersJson)
                            })
                            .ToList(),
                        Conditions = ability.Conditions
                            .OrderBy(x => x.DisplayOrder)
                            .Select(x => new GameConditionDefinition
                            {
                                Type = x.Type,
                                Value = x.Value,
                                Negate = x.Negate,
                                Parameters = ParseParameters(x.ParametersJson)
                            })
                            .ToList()
                    });
            }

            return card;
        }

        private static Dictionary<string, string> ParseParameters(
            string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}")
                return new Dictionary<string, string>();

            try
            {
                return System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }
    }
}
