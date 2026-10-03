using web_tcg.Data;
using web_tcg.Data.Entities;

namespace web_tcg.Services;

public static class DeckCardPersistence
{
    public static DeckCard Add(ApplicationDbContext context, Deck deck, int cardId, int quantity)
    {
        var entry = new DeckCard
        {
            Deck = deck,
            DeckId = deck.Id,
            CardId = cardId,
            Quantity = quantity
        };
        // Le GUID est déjà défini par DeckCard. Un simple ajout à la navigation
        // d'un deck chargé peut être détecté comme Modified au lieu de Added.
        context.Set<DeckCard>().Add(entry);
        return entry;
    }
}
