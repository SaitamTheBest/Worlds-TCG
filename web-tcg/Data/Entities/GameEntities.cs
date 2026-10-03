using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data.Enums;

namespace web_tcg.Data.Entities
{
    [Index(nameof(UserId), nameof(Name), IsUnique = true)]
    public class Deck
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public int LeaderCardId { get; set; }
        public Card LeaderCard { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<DeckCard> Cards { get; set; } = new List<DeckCard>();
    }

    [Index(nameof(DeckId), nameof(CardId), IsUnique = true)]
    public class DeckCard
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid DeckId { get; set; }
        public Deck Deck { get; set; } = null!;

        public int CardId { get; set; }
        public Card Card { get; set; } = null!;

        [Range(1, 4)]
        public int Quantity { get; set; }
    }

    public class GameMatch
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string PlayerOneId { get; set; } = string.Empty;

        [Required]
        public string PlayerTwoId { get; set; } = string.Empty;

        public Guid PlayerOneDeckId { get; set; }
        public Guid PlayerTwoDeckId { get; set; }

        public GameMatchStatus Status { get; set; } = GameMatchStatus.Starting;

        public string? WinnerUserId { get; set; }
        public GameEndReason? EndReason { get; set; }

        [Column(TypeName = "jsonb")]
        public string StateJson { get; set; } = "{}";

        public long StateVersion { get; set; }

        public bool RewardsGranted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public ICollection<GameLogEntry> Logs { get; set; } = new List<GameLogEntry>();
        public ICollection<GameChatMessage> ChatMessages { get; set; } = new List<GameChatMessage>();
    }

    public class GameLogEntry
    {
        public long Id { get; set; }

        public Guid GameMatchId { get; set; }
        public GameMatch GameMatch { get; set; } = null!;

        [MaxLength(450)]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class GameChatMessage
    {
        public long Id { get; set; }

        public Guid GameMatchId { get; set; }
        public GameMatch GameMatch { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required, MaxLength(300)]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CardAbilityEffect
    {
        public int Id { get; set; }

        public int CardAbilityId { get; set; }
        public CardAbility CardAbility { get; set; } = null!;

        public GameEffectType Type { get; set; }
        public GameEffectTrigger Trigger { get; set; }
        public GameTargetType Target { get; set; }

        public decimal Value { get; set; }

        /// <summary>
        /// 0 = permanent ou instantané selon l'effet.
        /// > 0 = durée exprimée en tours du propriétaire de la cible.
        /// </summary>
        public int DurationTurns { get; set; }

        [Column(TypeName = "jsonb")]
        public string ParametersJson { get; set; } = "{}";

        public int DisplayOrder { get; set; }
    }

    public class CardAbilityCondition
    {
        public int Id { get; set; }

        public int CardAbilityId { get; set; }
        public CardAbility CardAbility { get; set; } = null!;

        public GameConditionType Type { get; set; }

        public decimal Value { get; set; }

        public bool Negate { get; set; }
        [Column(TypeName = "jsonb")]
        public string ParametersJson { get; set; } = "{}";

        public int DisplayOrder { get; set; }
    }
}
