using web_tcg.Data.Enums;

namespace web_tcg.Models.Game
{
    public sealed class GameHandCostSelection
    {
        public int ConditionIndex { get; set; }
        public List<Guid> CardIds { get; set; } = new();
    }
    public sealed class GameHandCostDto
    {
        public int ConditionIndex { get; init; }
        public bool Discard { get; init; }
        public int Count { get; init; }
        public IReadOnlyList<Guid> EligibleIds { get; init; } = Array.Empty<Guid>();
    }
    public sealed class GameActionPreviewDto
    {
        public string UserId { get; init; } = string.Empty;
        public long Version { get; init; }
        public Guid? SourceId { get; init; }
        public Guid? TargetId { get; init; }
        public int? Slot { get; init; }
        public string Label { get; init; } = string.Empty;
        public bool IsAttack { get; init; }
        public bool Confirmed { get; init; }
    }

    public sealed class GameViewModel
    {
        public Guid MatchId { get; init; }
        public IReadOnlyList<web_tcg.Models.Card.CardViewModel> VisibleCards { get; init; } = Array.Empty<web_tcg.Models.Card.CardViewModel>();
    }

    public sealed class CreateGameRequest
    {
        public string OpponentUserId { get; set; } = string.Empty;
        public Guid MyDeckId { get; set; }
        public Guid OpponentDeckId { get; set; }
    }

    public sealed class GameStateDto
    {
        public Guid MatchId { get; init; }
        public long Version { get; init; }

        public GameMatchStatus Status { get; init; }

        public int TurnNumber { get; init; }

        public string ActivePlayerId { get; init; } = string.Empty;
        public string FirstPlayerId { get; init; } = string.Empty;

        public bool IsMyTurn { get; init; }

        public DateTime ServerTimeUtc { get; init; }
        public DateTime ClockAnchorUtc { get; init; }

        public GamePlayerDto Me { get; init; } = null!;
        public GamePlayerDto Opponent { get; init; } = null!;

        public string? WinnerUserId { get; init; }
        public GameEndReason? EndReason { get; init; }
        public int? XpEarned { get; init; }
        public decimal? CardBalanceEarned { get; init; }
    }

    public sealed class GamePlayerDto
    {
        public string UserId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? AvatarUrl { get; init; }

        public int Money { get; init; }

        public int RemainingSeconds { get; init; }
        public int TurnsPlayed { get; init; }

        public int HandCount { get; init; }
        public int DeckCount { get; init; }
        public int GraveyardCount { get; init; }

        public GameCardDto Leader { get; init; } = null!;
        public GameCardDto? Location { get; init; }

        public IReadOnlyList<GameCardDto?> Characters { get; init; } =
            Array.Empty<GameCardDto?>();

        /// <summary>
        /// Renseigné uniquement pour le joueur qui reçoit le DTO.
        /// La main adverse n'est jamais envoyée au navigateur.
        /// </summary>
        public IReadOnlyList<GameCardDto> Hand { get; init; } =
            Array.Empty<GameCardDto>();
    }

    public sealed class GameCardDto
    {
        public Guid InstanceId { get; init; }

        public int CardId { get; init; }

        public string Name { get; init; } = string.Empty;
        public CardType Type { get; init; }

        public string? ImageUrl { get; init; }

        public int UsageCost { get; init; }

        public int? MaxHealth { get; init; }
        public int? CurrentHealth { get; init; }
        public decimal DamageMultiplier { get; init; } = 1m;
        public int ExtraSkillUses { get; init; }
        public int DamageBonus { get; init; }

        public bool IsExhausted { get; init; }
        public bool IsImprisoned { get; init; }
        public bool IsBlocking { get; init; }
        public bool HasShield { get; init; }
        public bool IsStealthed { get; init; }
        public IReadOnlyList<GameTargetType> PlayTargetTypes { get; init; } = Array.Empty<GameTargetType>();
        public IReadOnlyList<GameHandCostDto> PlayHandCosts { get; init; } = Array.Empty<GameHandCostDto>();
        public IReadOnlyList<Guid> PlayTargetIds { get; init; } = Array.Empty<Guid>();

        public IReadOnlyList<GameAbilityDto> Abilities { get; init; } =
            Array.Empty<GameAbilityDto>();
    }

    public sealed class GameAbilityDto
    {
        public IReadOnlyList<GameHandCostDto> HandCosts { get; init; } = Array.Empty<GameHandCostDto>();
        public int Id { get; init; }
        public AbilityType Type { get; init; }

        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;

        public int UsageCost { get; init; }
        public int Cooldown { get; init; }
        public IReadOnlyList<GameTargetType> TargetTypes { get; init; } = Array.Empty<GameTargetType>();
        public IReadOnlyList<Guid> TargetIds { get; init; } = Array.Empty<Guid>();
        public bool IsAttack { get; init; }
        public string? UnavailableReason { get; init; }
        public string? TargetRefusalReason { get; init; }
    }
}
