using System.ComponentModel.DataAnnotations;
using web_tcg.Data.Enums;

namespace web_tcg.Models.Admin
{
    public class CreateCardViewModel
    {
        public int? Id { get; set; }

        public bool IsEdit =>
            Id.HasValue;

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } =
            string.Empty;

        [Required]
        public CardType Type { get; set; }

        public CardRarity? Rarity { get; set; }

        [Required]
        public int RegionId { get; set; }

        public int? CountryId { get; set; }

        [Required]
        public int CardSetId { get; set; }

        [Range(1, 999)]
        public int CollectionNumber { get; set; }

        [Range(0, 99)]
        public int UsageCost { get; set; }

        public int? HealthPoints { get; set; }

        /// <summary>
        /// Chemin interne de l'illustration principale.
        /// Exemple :
        /// /uploads/cards/6d48d6586fb24ae181951c8f10681b0e.png
        /// </summary>
        [Required(
            ErrorMessage =
                "L'illustration principale est obligatoire.")]
        [MaxLength(500)]
        [RegularExpression(
            @"^/uploads/cards/[a-f0-9]{32}\.(png|jpg|webp)$",
            ErrorMessage =
                "L'illustration principale doit être un fichier enregistré dans Worlds TCG.")]
        public string MainImageUrl { get; set; } =
            string.Empty;

        public List<CreateCardVariantViewModel> Variants { get; set; } =
            new();

        public List<CardAbilityInputModel> Abilities { get; set; } =
            new();
    }

    public class CardAbilityInputModel
    {
        public int? Id { get; set; }

        [Required]
        [EnumDataType(typeof(AbilityType))]
        public AbilityType Type { get; set; }

        [Range(0, 99)]
        public int UsageCost { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } =
            string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } =
            string.Empty;

        public int DisplayOrder { get; set; }

        [Range(0, 99)]
        public int CooldownTurns { get; set; }

        public List<CardEffectInputModel> Effects { get; set; } = new();
        public List<CardConditionInputModel> Conditions { get; set; } = new();
    }

    public class CardEffectInputModel
    {
        [EnumDataType(typeof(GameEffectType))] public GameEffectType Type { get; set; }
        [EnumDataType(typeof(GameEffectTrigger))] public GameEffectTrigger Trigger { get; set; }
        [EnumDataType(typeof(GameTargetType))] public GameTargetType Target { get; set; }
        [Range(0, 100000)] public decimal Value { get; set; }
        [Range(0, 99)] public int DurationTurns { get; set; }
        [MaxLength(4000)] public string ParametersJson { get; set; } = "{}";
        public List<int> RegionIds { get; set; } = new();
        public List<int> CountryIds { get; set; } = new();
        public bool ScaleByMatchingCharacters { get; set; }
        [Range(1, 3)] public int CountScope { get; set; } = 1;
        public List<int> CountRegionIds { get; set; } = new();
        public List<int> CountCountryIds { get; set; } = new();
    }

    public class CardConditionInputModel
    {
        [EnumDataType(typeof(GameConditionType))] public GameConditionType Type { get; set; }
        [Range(0, 100000)] public decimal Value { get; set; }
        public bool Negate { get; set; }
        public List<int> RegionIds { get; set; } = new();
        public List<int> CountryIds { get; set; } = new();
    }
}
