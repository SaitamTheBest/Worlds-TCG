using web_tcg.Data.Enums;

namespace web_tcg.Models.Deck
{
    public sealed class DeckSummaryDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string LeaderName { get; init; } = string.Empty;
        public int LeaderCardId { get; init; }
        public int TotalCards { get; init; }
        public bool IsComplete { get; init; }
    }

    public sealed class DeckDetailDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int LeaderCardId { get; init; }

        public List<DeckCardSelectionDto> Cards { get; init; } =
            new();
    }

    public sealed class DeckCardSelectionDto
    {
        public int CardId { get; init; }
        public int Quantity { get; init; }
    }

    public sealed class SaveDeckRequest
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int LeaderCardId { get; set; }

        public List<DeckCardSelectionDto> Cards { get; set; } =
            new();
    }

    /// <summary>
    /// Représente une carte de la collection du joueur,
    /// utilisable pour construire un deck.
    /// </summary>
    public sealed class CollectionCardForBuilderDto
    {
        public int CardId { get; init; }
        public string Name { get; init; } = string.Empty;
        public CardType Type { get; init; }
        public string? ImageUrl { get; init; }
        public CardArtType ArtType { get; init; }
        public int UsageCost { get; init; }
        public int OwnedQuantity { get; init; }

        /// <summary>
        /// Identifiant de la carte de gameplay (carte normale),
        /// utilisé pour regrouper les variantes entre elles
        /// (limite de 4 exemplaires cumulés).
        /// </summary>
        public int GameplayCardId { get; init; }
    }

    public sealed class DeckBuilderDataDto
    {
        public List<CollectionCardForBuilderDto> Leaders { get; init; } =
            new();

        public List<CollectionCardForBuilderDto> PlayableCards { get; init; } =
            new();
    }
}
