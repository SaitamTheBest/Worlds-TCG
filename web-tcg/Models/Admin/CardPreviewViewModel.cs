using System.Text.Json.Serialization;

namespace web_tcg.Models.Admin
{
    /// <summary>
    /// Données utilisées pour générer l'aperçu d'une carte.
    /// </summary>
    public class CardPreviewViewModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Nom de la carte";

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("rarity")]
        public int? Rarity { get; set; }

        [JsonPropertyName("usageCost")]
        public int UsageCost { get; set; }

        [JsonPropertyName("healthPoints")]
        public int? HealthPoints { get; set; }

        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; } =
            "https://via.placeholder.com/250x350?text=Carte";

        [JsonPropertyName("mainArtType")]
        public int MainArtType { get; set; }

        [JsonPropertyName("collectionNumber")]
        public string CollectionNumber { get; set; } = "XX/XX";

        [JsonPropertyName("regionName")]
        public string RegionName { get; set; } = "Région";

        [JsonPropertyName("politicalPartyId")]
        public int? PoliticalPartyId { get; set; }

        [JsonPropertyName("countryName")]
        public string CountryName { get; set; } = string.Empty;

        [JsonPropertyName("regionColor")]
        public string RegionColor { get; set; } = "#FFD700";

        [JsonPropertyName("cardSetCode")]
        public string CardSetCode { get; set; } = string.Empty;

        [JsonPropertyName("abilities")]
        public List<CardPreviewAbilityViewModel> Abilities { get; set; }
            = new();
    }

    /// <summary>
    /// Données d'une capacité dans l'aperçu d'une carte.
    /// </summary>
    public class CardPreviewAbilityViewModel
    {
        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("usageCost")]
        public int UsageCost { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("displayOrder")]
        public int DisplayOrder { get; set; }
    }
}
