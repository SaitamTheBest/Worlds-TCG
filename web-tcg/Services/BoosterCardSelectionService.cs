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

            query = rarity switch
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

            var count = await query.CountAsync();

            if (count == 0)
            {
                throw new InvalidOperationException(
                    $"Aucune carte disponible pour le drop {rarity} dans l'extension {cardSetId}.");
            }

            var index = Random.Shared.Next(count);

            return await query
                .OrderBy(x => x.Id)
                .Skip(index)
                .FirstAsync();
        }
    }
}