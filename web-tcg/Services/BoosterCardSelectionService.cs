using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;

namespace web_tcg.Services
{
    public class BoosterCardSelectionService
    {
        private readonly ApplicationDbContext _context;

        public BoosterCardSelectionService(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Card> SelectCardAsync(
            BoosterRarity rarity,
            int cardSetId)
        {
            IQueryable<Card> query = _context.Cards
                .AsNoTracking()
                .Where(x =>
                    x.CardSetId == cardSetId &&
                    x.IsActive);

            query = FilterCards(query, rarity);

            var count = await query.CountAsync();
            if (count == 0)
                throw new InvalidOperationException($"Aucune carte disponible pour le drop {rarity} dans l'extension {cardSetId}.");

            return await query.OrderBy(x => x.Id).Skip(Random.Shared.Next(count)).FirstAsync();
        }

        // Partagé par le tirage et l'affichage des taux : mêmes cartes éligibles.
        public static IQueryable<Card> FilterCards(IQueryable<Card> query, BoosterRarity rarity)
        {
            if (rarity == BoosterRarity.Secret)
                return query.Where(x => x.Type == CardType.Secret && x.CollectionNumber == 0 && x.BaseCardId == null);
            query = query.Where(x => x.Type != CardType.Secret);
            return rarity switch
            {
                BoosterRarity.Common =>
                    query.Where(x =>
                        x.BaseCardId == null &&
                        x.ArtType == CardArtType.Normal &&
                        x.Type != CardType.Leader &&
                        x.Rarity == CardRarity.Common),

                BoosterRarity.Rare =>
                    query.Where(x =>
                        x.BaseCardId == null &&
                        x.ArtType == CardArtType.Normal &&
                        x.Type != CardType.Leader &&
                        x.Rarity == CardRarity.Rare),

                BoosterRarity.Leader =>
                    query.Where(x =>
                        x.BaseCardId == null &&
                        x.ArtType == CardArtType.Normal &&
                        x.Type == CardType.Leader),

                BoosterRarity.SuperRare =>
                    query.Where(x =>
                        x.BaseCardId == null &&
                        x.ArtType == CardArtType.Normal &&
                        x.Type != CardType.Leader &&
                        x.Rarity == CardRarity.SuperRare),

                BoosterRarity.UltraRare =>
                    query.Where(x =>
                        x.BaseCardId == null &&
                        x.ArtType == CardArtType.Normal &&
                        x.Type != CardType.Leader &&
                        x.Rarity == CardRarity.UltraRare),

                BoosterRarity.AlternateArt =>
                    query.Where(x =>
                        x.BaseCardId != null &&
                        x.ArtType == CardArtType.AlternateArt),

                BoosterRarity.FullArt =>
                    query.Where(x =>
                        x.BaseCardId != null &&
                        x.ArtType == CardArtType.FullArt),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(rarity),
                    rarity,
                    "Rareté de booster inconnue.")
            };

        }
    }
}
