using web_tcg.Data.Enums;

namespace web_tcg.Game.Runtime
{
    public sealed class GameState
    {
        public Guid MatchId { get; set; }
        public long Version { get; set; }

        public GameMatchStatus Status { get; set; } = GameMatchStatus.Starting;

        public int TurnNumber { get; set; }

        public string FirstPlayerId { get; set; } = string.Empty;
        public string ActivePlayerId { get; set; } = string.Empty;

        public DateTime TurnStartedAtUtc { get; set; }
        public DateTime ClockAnchorUtc { get; set; }
        public DateTime LastActionAtUtc { get; set; }

        public string? WinnerUserId { get; set; }
        public GameEndReason? EndReason { get; set; }

        // Montants effectivement crédités, conservés avec la partie.
        public int? RewardXp { get; set; }
        public decimal? RewardCardBalance { get; set; }

        public GamePlayerState PlayerOne { get; set; } = new();
        [System.Text.Json.Serialization.JsonIgnore]
        public List<GameCardState> ActionReveals { get; } = new();
        public GamePlayerState PlayerTwo { get; set; } = new();

        public GamePlayerState GetPlayer(string userId)
        {
            if (PlayerOne.UserId == userId)
                return PlayerOne;

            if (PlayerTwo.UserId == userId)
                return PlayerTwo;

            throw new InvalidOperationException("Le joueur ne participe pas à cette partie.");
        }

        public GamePlayerState GetOpponent(string userId)
        {
            if (PlayerOne.UserId == userId)
                return PlayerTwo;

            if (PlayerTwo.UserId == userId)
                return PlayerOne;

            throw new InvalidOperationException("Le joueur ne participe pas à cette partie.");
        }

        public GameCardState? FindCard(Guid instanceId)
        {
            return EnumerateCards()
                .FirstOrDefault(x => x.InstanceId == instanceId);
        }

        public IEnumerable<GameCardState> EnumerateCards()
        {
            foreach (var player in new[] { PlayerOne, PlayerTwo })
            {
                yield return player.Leader;

                if (player.Location != null)
                    yield return player.Location;

                foreach (var card in player.Characters)
                {
                    if (card != null)
                        yield return card;
                }

                foreach (var card in player.Hand)
                    yield return card;

                foreach (var card in player.Deck)
                    yield return card;

                foreach (var card in player.Graveyard)
                    yield return card;
            }
        }
    }

    public sealed class GamePlayerState
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        public int Money { get; set; }
        public int RemainingSeconds { get; set; } = 20 * 60;
        public int TurnsPlayed { get; set; }

        public GameCardState Leader { get; set; } = null!;
        public GameCardState? Location { get; set; }

        public List<GameCardState?> Characters { get; set; } =
            new() { null, null, null, null };

        public List<GameCardState> Deck { get; set; } = new();
        public List<GameCardState> Hand { get; set; } = new();
        public List<GameCardState> Graveyard { get; set; } = new();
    }

    public sealed class GameCardState
    {
        public Guid InstanceId { get; set; } = Guid.NewGuid();

        public int CardId { get; set; }
        public int GameplayCardId { get; set; }
        public int RegionId { get; set; }
        public int? CountryId { get; set; }
        public int? PoliticalPartyId { get; set; }
        public PoliticalOrientation? PoliticalPartyOrientation { get; set; }

        public string OwnerUserId { get; set; } = string.Empty;
        public string ControllerUserId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public CardType Type { get; set; }
        public CardRarity? Rarity { get; set; }

        public string? ImageUrl { get; set; }

        public int UsageCost { get; set; }

        public int? MaxHealth { get; set; }
        public int? BaseHealth { get; set; }
        public int? CurrentHealth { get; set; }

        public GameCardZone Zone { get; set; }
        public int? BoardSlot { get; set; }

        public bool IsExhausted { get; set; }
        public bool IsImprisoned => AppliedEffects.Any(e => e.Type == GameEffectType.Prison);
        public int ReadyOnOwnerTurn { get; set; }

        public bool IsBlocking { get; set; }
        public int? BlockExpiresAfterTurn { get; set; }

        public bool HasShield { get; set; }
        public bool IsStealthed { get; set; }

        public int SkillUsesThisTurn { get; set; }
        public int ExtraSkillUses { get; set; }

        public decimal DamageMultiplier { get; set; } = 1m;
        public int DamageBonus { get; set; }
        public bool HasRevived { get; set; }

        public Dictionary<int, int> AbilityCooldowns { get; set; } = new();

        public List<GameAbilityState> Abilities { get; set; } = new();
        public List<GameAppliedEffect> AppliedEffects { get; set; } = new();
    }

    public sealed class GameAbilityState
    {
        public int Id { get; set; }
        public AbilityType Type { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int UsageCost { get; set; }
        public int CooldownTurns { get; set; }

        public List<GameEffectDefinition> Effects { get; set; } = new();
        public List<GameConditionDefinition> Conditions { get; set; } = new();
    }

    public sealed class GameEffectDefinition
    {
        public GameEffectType Type { get; set; }
        public GameEffectTrigger Trigger { get; set; }
        public GameTargetType Target { get; set; }

        public decimal Value { get; set; }
        public int DurationTurns { get; set; }

        public Dictionary<string, string> Parameters { get; set; } = new();
    }

    public sealed class GameConditionDefinition
    {
        public GameConditionType Type { get; set; }
        public decimal Value { get; set; }
        public bool Negate { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
    }

    public sealed class GameAppliedEffect
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public GameEffectType Type { get; set; }
        public decimal Value { get; set; }

        public int? SourceCardId { get; set; }
        public Guid? SourceInstanceId { get; set; }
        public string? DefinitionKey { get; set; }

        public int? ExpiresOnOwnerTurn { get; set; }
    }
}
