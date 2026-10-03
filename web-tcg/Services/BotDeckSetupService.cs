using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;

namespace web_tcg.Services;

public sealed class BotDeckSetupService(ApplicationDbContext context, ILogger<BotDeckSetupService> logger)
{
    public async Task EnsureAsync(string botUserId)
    {
        var deck = await context.Set<Deck>().Include(d => d.Cards).FirstOrDefaultAsync(d => d.UserId == botUserId);
        if (deck == null || deck.Cards.Sum(c => c.Quantity) != 40)
        {
            var leader = await context.Cards.FirstOrDefaultAsync(c => c.Type == CardType.Leader && c.BaseCardId == null && c.IsActive);
            var cards = await context.Cards.Where(c => c.Type != CardType.Leader && c.BaseCardId == null && c.IsActive)
                .OrderBy(c => c.Id).Take(20).ToListAsync();
            if (leader == null || cards.Count < 10)
            {
                logger.LogWarning("Deck IA indisponible : il faut un Leader et au moins 10 cartes principales distinctes.");
                return;
            }
            if (deck == null)
            {
                deck = new Deck { UserId = botUserId, Name = "Deck IA", LeaderCardId = leader.Id };
                context.Set<Deck>().Add(deck);
            }
            deck.LeaderCardId = leader.Id;
            var remaining = 40;
            var quantities = new Dictionary<int, int>();
            for (var i = 0; i < cards.Count; i++)
            {
                var quantity = Math.Min(4, (int)Math.Ceiling((double)remaining / (cards.Count - i)));
                quantities[cards[i].Id] = quantity;
                remaining -= quantity;
            }
            foreach (var entry in deck.Cards.ToList())
            {
                if (quantities.TryGetValue(entry.CardId, out var quantity)) entry.Quantity = quantity;
                else { context.Set<DeckCard>().Remove(entry); deck.Cards.Remove(entry); }
            }
            foreach (var entry in quantities.Where(q => !deck.Cards.Any(c => c.CardId == q.Key)))
                DeckCardPersistence.Add(context, deck, entry.Key, entry.Value);
        }
        // Le bot suit les mêmes règles de possession que les joueurs.
        var required = deck.Cards.ToDictionary(c => c.CardId, c => c.Quantity);
        required[deck.LeaderCardId] = 1;
        var inventory = await context.UserCardCollections.Where(c => c.UserId == botUserId).ToDictionaryAsync(c => c.CardId);
        foreach (var entry in required)
        {
            if (inventory.TryGetValue(entry.Key, out var owned)) owned.Quantity = Math.Max(owned.Quantity, entry.Value);
            else context.UserCardCollections.Add(new UserCardCollection { UserId = botUserId, CardId = entry.Key, Quantity = entry.Value });
        }
        await context.SaveChangesAsync();
    }
}
