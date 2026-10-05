using web_tcg.Data.Enums;

namespace web_tcg.Game
{
    public sealed class BoosterDropConfiguration
    {
        public int CardCount { get; init; } = 5;

        public IReadOnlyDictionary<BoosterRarity, double> BaseRates { get; init; } =
            new Dictionary<BoosterRarity, double>
            {
                [BoosterRarity.Common] = 79.30,
                [BoosterRarity.Rare] = 17.00,
                [BoosterRarity.SuperRare] = 2.70,
                [BoosterRarity.UltraRare] = 0.50,
                [BoosterRarity.AlternateArt] = 0.40,
                [BoosterRarity.FullArt] = 0.10
            };
    }

    public sealed class BoosterPositionConfiguration
    {
        public IReadOnlyDictionary<int, double> HitMultiplier { get; init; } =
            new Dictionary<int, double>
            {
                [1] = 0.35,
                [2] = 0.55,
                [3] = 0.85,
                [4] = 1.35,
                [5] = 2.20
            };
    }

    public sealed class BoosterRarityGenerator
    {
        private readonly Random _random;

        private static readonly BoosterRarity[] Rarities =
        {
            BoosterRarity.Common,
            BoosterRarity.Rare,
            BoosterRarity.Leader,
            BoosterRarity.SuperRare,
            BoosterRarity.UltraRare,
            BoosterRarity.AlternateArt,
            BoosterRarity.FullArt,
            BoosterRarity.Secret
        };

        public BoosterRarityGenerator(Random? random = null)
        {
            _random = random ?? Random.Shared;
        }

        public BoosterRarity Generate(
            int cardPosition,
            BoosterRarity? minimumRarity = null, bool includeSecret = false)
        {
            var rates = GetRatesForPosition(
                cardPosition,
                minimumRarity, includeSecret);

            var roll =
                _random.NextDouble() *
                rates.Values.Sum();

            double cumulative = 0;

            foreach (var rarity in Rarities)
            {
                if (!rates.TryGetValue(
                        rarity,
                        out var rate))
                {
                    continue;
                }

                cumulative += rate;

                if (roll < cumulative)
                    return rarity;
            }

            return Rarities.Last(rarity => rates.TryGetValue(rarity, out var rate) && rate > 0);
        }

        public Dictionary<BoosterRarity, double> GetRatesForPosition(
            int position,
            BoosterRarity? minimumRarity, bool includeSecret = false)
        {
            var rates =
                GetPositionRates(position);

            if (minimumRarity.HasValue)
            {
                rates = rates
                    .Where(x =>
                        x.Key >= minimumRarity.Value)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value);
            }

            rates = Normalize(rates);
            // Pourcentage fixe après la garantie Rare : 0,01 %, jamais amplifié par la normalisation.
            if (includeSecret)
            {
                foreach (var rarity in rates.Keys.ToList()) rates[rarity] *= .9999;
                rates[BoosterRarity.Secret] = .01;
            }
            return rates;
        }

        private Dictionary<BoosterRarity, double> GetPositionRates(
            int position)
        {
            return position switch
            {
                /*
                 * Première carte :
                 * très majoritairement commune,
                 * mais une belle surprise reste possible.
                 */
                1 => new()
                {
                    [BoosterRarity.Common] = 92.50,
                    [BoosterRarity.Rare] = 6.40,
                    [BoosterRarity.Leader] = 0.20,
                    [BoosterRarity.SuperRare] = 0.65,
                    [BoosterRarity.UltraRare] = 0.15,
                    [BoosterRarity.AlternateArt] = 0.08,
                    [BoosterRarity.FullArt] = 0.02
                },

                /*
                 * Deuxième carte :
                 * les chances de hit commencent légèrement à monter.
                 */
                2 => new()
                {
                    [BoosterRarity.Common] = 87.00,
                    [BoosterRarity.Rare] = 10.85,
                    [BoosterRarity.Leader] = 0.30,
                    [BoosterRarity.SuperRare] = 1.35,
                    [BoosterRarity.UltraRare] = 0.30,
                    [BoosterRarity.AlternateArt] = 0.15,
                    [BoosterRarity.FullArt] = 0.05
                },

                /*
                 * Troisième carte :
                 * chances intermédiaires.
                 */
                3 => new()
                {
                    [BoosterRarity.Common] = 78.90,
                    [BoosterRarity.Rare] = 16.80,
                    [BoosterRarity.Leader] = 0.50,
                    [BoosterRarity.SuperRare] = 3.00,
                    [BoosterRarity.UltraRare] = 0.55,
                    [BoosterRarity.AlternateArt] = 0.20,
                    [BoosterRarity.FullArt] = 0.05
                },

                /*
                 * Quatrième carte :
                 * vraie possibilité de commencer à toucher
                 * une carte plus prestigieuse.
                 */
                4 => new()
                {
                    [BoosterRarity.Common] = 64.20,
                    [BoosterRarity.Rare] = 27.80,
                    [BoosterRarity.Leader] = 0.80,
                    [BoosterRarity.SuperRare] = 5.70,
                    [BoosterRarity.UltraRare] = 1.10,
                    [BoosterRarity.AlternateArt] = 0.30,
                    [BoosterRarity.FullArt] = 0.10
                },

                /*
                 * Dernière carte :
                 * Rare minimum.
                 *
                 * Les probabilités des cartes visuellement
                 * prestigieuses sont légèrement augmentées,
                 * sans rendre les Alt Art / Full Art communes.
                 */
                5 => new()
                {
                    [BoosterRarity.Common] = 0.00,
                    [BoosterRarity.Rare] = 93.80,
                    [BoosterRarity.Leader] = 1.50,
                    [BoosterRarity.SuperRare] = 3.50,
                    [BoosterRarity.UltraRare] = 0.70,
                    [BoosterRarity.AlternateArt] = 0.35,
                    [BoosterRarity.FullArt] = 0.15
                },

                _ => throw new ArgumentOutOfRangeException(
                    nameof(position))
            };
        }

        private static Dictionary<BoosterRarity, double> Normalize(
            Dictionary<BoosterRarity, double> rates)
        {
            var total =
                rates.Values.Sum();

            if (total <= 0)
            {
                throw new InvalidOperationException(
                    "Aucun taux de drop disponible.");
            }

            return rates.ToDictionary(
                x => x.Key,
                x => x.Value / total * 100);
        }
    }
}
