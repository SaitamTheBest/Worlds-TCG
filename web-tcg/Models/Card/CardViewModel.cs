using web_tcg.Data.Entities;
using web_tcg.Data.Enums;

namespace web_tcg.Models.Card
{
    /// <summary>
    /// Données nécessaires au rendu visuel d'une carte.
    /// </summary>
    public class CardViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public CardType Type { get; set; }

        public CardRarity? Rarity { get; set; }

        public CardArtType ArtType { get; set; }

        public string CollectionNumber { get; set; } = string.Empty;

        public string RegionName { get; set; } = string.Empty;

        public string RegionColor { get; set; } = "#F4C430";

        public string? CountryName { get; set; }
        public string? PoliticalPartyName { get; set; }
        public PoliticalOrientation? PoliticalPartyOrientation { get; set; }
        public string? PoliticalPartyImageUrl { get; set; }

        public string CardSetCode { get; set; } = string.Empty;

        public int UsageCost { get; set; }

        public int? HealthPoints { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public List<CardAbilityViewModel> Abilities { get; set; } = new();
    }

    /// <summary>
    /// Données d'une compétence, d'un trait ou d'un effet.
    /// </summary>
    public class CardAbilityViewModel
    {
        public AbilityType Type { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int UsageCost { get; set; }
        public int DisplayOrder { get; set; }
    }
}
