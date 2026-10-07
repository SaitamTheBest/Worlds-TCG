using System.ComponentModel.DataAnnotations;
using web_tcg.Data.Enums;

namespace web_tcg.Models.Admin
{
    public class EditCardViewModel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public CardType Type { get; set; }

        public CardRarity? Rarity { get; set; }

        [Required]
        public int RegionId { get; set; }

        public int? CountryId { get; set; }

        [Required]
        public int CardSetId { get; set; }

        [Required]
        [MaxLength(50)]
        public string CollectionNumber { get; set; } = string.Empty;

        [Range(0, 999)]
        public int UsageCost { get; set; }

        public int? HealthPoints { get; set; }

        [Required]
        [MaxLength(500)]
        public string MainImageUrl { get; set; } = string.Empty;

        [Required]
        public CardArtType MainArtType { get; set; }

        public string? SecondaryImageUrl { get; set; }

        public CardArtType? SecondaryArtType { get; set; }

        public List<EditCardAbilityViewModel> Abilities { get; set; } = new();

        // ----------------------------------------------------
        // Données nécessaires aux listes déroulantes
        // ----------------------------------------------------

        public List<CardSetOptionViewModel> CardSets { get; set; } = new();

        public List<RegionOptionViewModel> Regions { get; set; } = new();

        public List<CountryOptionViewModel> Countries { get; set; } = new();
    }

    public class EditCardAbilityViewModel
    {
        public int? Id { get; set; }

        [Required]
        public AbilityType Type { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Range(0, 750)]
        public int UsageCost { get; set; }

        public int DisplayOrder { get; set; }
    }

    public class CardSetOptionViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;
    }

    public class RegionOptionViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Color { get; set; } = "#FFFFFF";
    }

    public class CountryOptionViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public int RegionId { get; set; }
    }
}
