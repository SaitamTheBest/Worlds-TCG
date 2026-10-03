using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Engine;

namespace web_tcg.Services
{
    public sealed class DeckValidationService
    {
        private readonly ApplicationDbContext _context;

        public DeckValidationService(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task ValidateAsync(
            Guid deckId,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var deck =
                await _context.Set<Deck>()
                    .AsNoTracking()
                    .Include(x => x.Cards)
                        .ThenInclude(x => x.Card)
                    .Include(x => x.LeaderCard)
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == deckId &&
                            x.UserId == userId,
                        cancellationToken)
                ?? throw new GameRuleException(
                    "Deck introuvable.");

            var total =
                deck.Cards.Sum(
                    x => x.Quantity);

            if (deck.Cards.Any(x => x.Quantity < 1 || x.Quantity > 4) ||
                deck.Cards.Select(x => x.CardId).Distinct().Count() != deck.Cards.Count)
                throw new GameRuleException("Les quantités des cartes du deck sont invalides.");

            if (total != 40)
            {
                throw new GameRuleException(
                    $"Le deck doit contenir exactement 40 cartes. Actuellement : {total}.");
            }

            var leaderGameplayId =
                deck.LeaderCard.BaseCardId ??
                deck.LeaderCard.Id;

            var leaderGameplay =
                await _context.Cards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                            leaderGameplayId,
                        cancellationToken);

            if (leaderGameplay?.Type != CardType.Leader)
            {
                throw new GameRuleException(
                    "La carte Leader du deck est invalide.");
            }

            foreach (var deckCard in deck.Cards)
            {
                var gameplayId =
                    deckCard.Card.BaseCardId ??
                    deckCard.Card.Id;

                var gameplayCard =
                    await _context.Cards
                        .AsNoTracking()
                        .FirstAsync(
                            x =>
                                x.Id ==
                                gameplayId,
                            cancellationToken);

                if (gameplayCard.Type ==
                    CardType.Leader)
                {
                    throw new GameRuleException(
                        "Le deck principal ne peut pas contenir de Leader.");
                }
            }

            /*
             * Limite de 4 exemplaires par carte de gameplay.
             * Normal + Alternate Art + Full Art comptent ensemble.
             */
            var grouped =
                deck.Cards
                    .GroupBy(x =>
                        x.Card.BaseCardId ??
                        x.Card.Id);

            foreach (var group in grouped)
            {
                if (group.Sum(x => x.Quantity) > 4)
                {
                    throw new GameRuleException(
                        "Une même carte ne peut apparaître que 4 fois dans un deck, variantes comprises.");
                }
            }

            var inventory =
                await _context.UserCardCollections
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == userId &&
                        x.Quantity > 0)
                    .ToDictionaryAsync(
                        x => x.CardId,
                        x => x.Quantity,
                        cancellationToken);

            if (!inventory.TryGetValue(
                    deck.LeaderCardId,
                    out var leaderQuantity) ||
                leaderQuantity < 1)
            {
                throw new GameRuleException(
                    "Vous ne possédez pas le Leader sélectionné.");
            }

            foreach (var entry in deck.Cards)
            {
                if (!inventory.TryGetValue(
                        entry.CardId,
                        out var owned) ||
                    owned < entry.Quantity)
                {
                    throw new GameRuleException(
                        $"Vous ne possédez pas suffisamment d'exemplaires de {entry.Card.Name}.");
                }
            }
        }
    }
}
