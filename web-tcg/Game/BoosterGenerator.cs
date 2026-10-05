using web_tcg.Data.Enums;

namespace web_tcg.Game
{
    public sealed class BoosterGenerator
    {
        private readonly BoosterRarityGenerator _rarityGenerator;

        public BoosterGenerator(BoosterRarityGenerator rarityGenerator)
        {
            _rarityGenerator = rarityGenerator;
        }

        /// <summary>Probabilité moyenne par carte, avec la garantie du dernier emplacement.</summary>
        public IReadOnlyDictionary<BoosterRarity, double> GetDropRates(int cardCount = 5, bool includeSecret = false)
        {
            if (cardCount <= 0) throw new ArgumentOutOfRangeException(nameof(cardCount));
            var rates = Enum.GetValues<BoosterRarity>().ToDictionary(r => r, _ => 0d);
            for (var position = 1; position <= cardCount; position++)
            {
                var positionRates = _rarityGenerator.GetRatesForPosition(position,
                    position == cardCount ? BoosterRarity.Rare : null, includeSecret);
                foreach (var entry in positionRates) rates[entry.Key] += entry.Value / cardCount;
            }
            return rates;
        }

        /// <summary>
        /// Génère un booster.
        ///
        /// Chaque carte est tirée indépendamment.
        /// La dernière carte générée est au minimum Rare.
        ///
        /// Une fois le booster généré, les cartes sont triées
        /// de la rareté la plus faible à la plus élevée afin que
        /// le meilleur hit soit révélé en dernier.
        /// </summary>
        public BoosterResult Generate(int cardCount = 5, bool includeSecret = false)
        {
            if (cardCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(cardCount));

            var generatedRarities = new List<BoosterRarity>(cardCount);

            for (var position = 1; position <= cardCount; position++)
            {
                BoosterRarity? minimumRarity = null;

                // La dernière carte générée est garantie Rare minimum.
                if (position == cardCount)
                    minimumRarity = BoosterRarity.Rare;

                var rarity = _rarityGenerator.Generate(
                    position,
                    minimumRarity, includeSecret);

                generatedRarities.Add(rarity);
            }

            // Tri :
            // Common
            // Rare
            // SuperRare
            // UltraRare
            // AlternateArt
            // FullArt
            var sortedRarities = generatedRarities
                .OrderBy(x => x)
                .ToList();

            var cards = sortedRarities
                .Select((rarity, index) => new BoosterCardResult
                {
                    Position = index + 1,
                    Rarity = rarity
                })
                .ToList();

            return new BoosterResult(cards);
        }
    }

    public sealed class BoosterCardResult
    {
        public int Position { get; init; }

        public BoosterRarity Rarity { get; init; }
    }

    public sealed class BoosterResult
    {
        public IReadOnlyList<BoosterCardResult> Cards { get; }

        public BoosterResult(IReadOnlyList<BoosterCardResult> cards)
        {
            Cards = cards;
        }

        public BoosterRarity HighestRarity =>
            Cards.Max(x => x.Rarity);
    }
}
