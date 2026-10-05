using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Models.Card;

namespace web_tcg.Services;

public sealed class CardPresentationService(ApplicationDbContext context)
{
    public async Task<List<CardViewModel>> GetAsync(IEnumerable<int> cardIds)
    {
        // List.Contains évite la surcharge MemoryExtensions.Contains(ReadOnlySpan)
        // sélectionnée pour les tableaux en C# 14, incompatible avec EF Core 9.
        var ids = cardIds.Distinct().ToList();
        if (ids.Count == 0) return new List<CardViewModel>();
        var cards = await context.Cards.AsNoTracking().Where(c => ids.Contains(c.Id))
            .Include(c => c.Region).Include(c => c.Country)
            .Include(c => c.PoliticalParty).Include(c => c.CardSet)
            .Include(c => c.Abilities).Include(c => c.BaseCard).ThenInclude(c => c!.Abilities)
            .ToListAsync();
        var gameplayIds = cards.Select(c => c.BaseCardId ?? c.Id).Distinct().ToList();
        var leaderHp = await context.LeaderCards.AsNoTracking().Where(c => gameplayIds.Contains(c.CardId)).ToDictionaryAsync(c => c.CardId, c => c.HealthPoints);
        var characterHp = await context.CharacterCards.AsNoTracking().Where(c => gameplayIds.Contains(c.CardId)).ToDictionaryAsync(c => c.CardId, c => c.HealthPoints);
        return cards.Select(card =>
        {
            var gameplay = card.BaseCard ?? card;
            int? hp = leaderHp.TryGetValue(gameplay.Id, out var leaderHealth) ? leaderHealth
                : characterHp.TryGetValue(gameplay.Id, out var characterHealth) ? characterHealth : null;
            return new CardViewModel
            {
                Id = card.Id, Name = card.Name, Type = gameplay.Type, Rarity = card.Rarity,
                ArtType = card.ArtType, UsageCost = gameplay.UsageCost, HealthPoints = hp,
                ImageUrl = card.ImageUrl ?? string.Empty,
                CollectionNumber = card.CollectionNumber + (card.VariantNumber.HasValue ? $"-{card.VariantNumber}" : ""),
                RegionName = card.Region?.Name ?? "Région", RegionColor = card.Region?.Color ?? "#F4C430",
                PoliticalPartyName = card.PoliticalParty?.Name,
                PoliticalPartyOrientation = card.PoliticalParty?.Orientation,
                PoliticalPartyImageUrl = card.PoliticalParty?.ImageUrl,
                CountryName = card.Country?.Name, CardSetCode = card.CardSet?.Code ?? string.Empty,
                Abilities = gameplay.Abilities.OrderBy(a => a.DisplayOrder).Select(a => new CardAbilityViewModel
                {
                    Type = a.Type, Name = a.Name, Description = a.Description,
                    UsageCost = a.UsageCost, DisplayOrder = a.DisplayOrder
                }).ToList()
            };
        }).ToList();
    }
}
