using System.ComponentModel.DataAnnotations;
using web_tcg.Data.Enums;

namespace web_tcg.Data.Entities
{
    public class CardAbility
    {
        public int Id { get; set; }

        public int CardId { get; set; }
        public Card Card { get; set; } = null!;

        public AbilityType Type { get; set; }

        [Range(0, 999)]
        public int UsageCost { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        /// <summary>
        /// Utilisé principalement par les talents.
        /// 0 = utilisable immédiatement.
        /// </summary>
        [Range(0, 99)]
        public int CooldownTurns { get; set; }

        public ICollection<CardAbilityEffect> Effects { get; set; } =
            new List<CardAbilityEffect>();

        public ICollection<CardAbilityCondition> Conditions { get; set; } =
            new List<CardAbilityCondition>();
    }
}