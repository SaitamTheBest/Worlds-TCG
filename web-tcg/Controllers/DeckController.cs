using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Models.Deck;
using web_tcg.Services;
using DeckEntity = web_tcg.Data.Entities.Deck;

namespace web_tcg.Controllers
{
    [Authorize]
    [Route("Deck")]
    public sealed class DeckController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DeckController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        private string UserId =>
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException(
                "Utilisateur non authentifié.");

        [HttpGet("MyDecks")]
        public async Task<IActionResult> GetMyDecks()
        {
            var decks =
                await _context.Set<DeckEntity>()
                    .AsNoTracking()
                    .Where(x => x.UserId == UserId)
                    .Include(x => x.LeaderCard)
                    .Include(x => x.Cards)
                    .OrderBy(x => x.Name)
                    .ToListAsync();

            var result =
                decks.Select(x => new DeckSummaryDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    LeaderName = x.LeaderCard.Name,
                    LeaderCardId = x.LeaderCardId,
                    TotalCards = x.Cards.Sum(c => c.Quantity),
                    IsComplete = x.Cards.Sum(c => c.Quantity) == 40
                })
                .ToList();

            return Json(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetDeck(
            Guid id)
        {
            var deck =
                await _context.Set<DeckEntity>()
                    .AsNoTracking()
                    .Include(x => x.Cards)
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        x.UserId == UserId);

            if (deck == null)
                return NotFound();

            var result =
                new DeckDetailDto
                {
                    Id = deck.Id,
                    Name = deck.Name,
                    LeaderCardId = deck.LeaderCardId,
                    Cards = deck.Cards
                        .Select(x => new DeckCardSelectionDto
                        {
                            CardId = x.CardId,
                            Quantity = x.Quantity
                        })
                        .ToList()
                };

            return Json(result);
        }

        /// <summary>
        /// Retourne les cartes possédées par le joueur,
        /// utilisables pour construire un deck.
        /// </summary>
        [HttpGet("BuilderData")]
        public async Task<IActionResult> GetBuilderData()
        {
            var collection =
                await _context.UserCardCollections
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == UserId &&
                        x.Quantity > 0 && x.Card.Type != CardType.Secret && x.Card.CollectionNumber != 0)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.BaseCard)
                    .Include(x => x.Card).ThenInclude(x => x.CardSet)
                    .ToListAsync();

            var items =
                collection.Select(x =>
                {
                    var card = x.Card;
                    var gameplayCard = card.BaseCard ?? card;
                    var gameplayCardId = card.BaseCardId ?? card.Id;

                    return new CollectionCardForBuilderDto
                    {
                        CardId = card.Id,
                        Name = card.Name,
                        Type = gameplayCard.Type,
                        ImageUrl = card.ImageUrl,
                        ArtType = card.ArtType,
                        UsageCost = gameplayCard.UsageCost,
                        OwnedQuantity = x.Quantity,
                        CardSetId = card.CardSetId,
                        CardSetName = card.CardSet.Name,
                        CardSetCode = card.CardSet.Code,
                        CardNumber = $"{card.CardSet.Code}-{card.CollectionNumber}" + (card.VariantNumber.HasValue ? $"-{card.VariantNumber}" : ""),
                        GameplayCardId = gameplayCardId
                    };
                })
                .ToList();

            var result =
                new DeckBuilderDataDto
                {
                    Leaders = items
                        .Where(x => x.Type == CardType.Leader)
                        .OrderBy(x => x.Name)
                        .ToList(),

                    PlayableCards = items
                        .Where(x => x.Type != CardType.Leader)
                        .OrderBy(x => x.Name)
                        .ToList()
                };

            return Json(result);
        }

        [HttpPost("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            [FromBody] SaveDeckRequest request)
        {
            if (!ModelState.IsValid || request == null || request.Cards == null)
                return BadRequest("Les données du deck sont invalides.");

            request.Name = request.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(request.Name) ||
                request.Name.Length > 100)
            {
                return BadRequest(
                    "Le nom du deck est invalide.");
            }

            var leaderOwned =
                await _context.UserCardCollections
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.UserId == UserId &&
                        x.CardId == request.LeaderCardId &&
                        x.Quantity > 0);

            if (!leaderOwned)
            {
                return BadRequest(
                    "Vous ne possédez pas ce Leader.");
            }

            var leaderCard =
                await _context.Cards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == request.LeaderCardId);

            var leaderGameplay =
                leaderCard?.BaseCardId != null
                    ? await _context.Cards
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.Id == leaderCard.BaseCardId)
                    : leaderCard;

            if (leaderGameplay?.Type != CardType.Leader)
            {
                return BadRequest(
                    "La carte sélectionnée n'est pas un Leader valide.");
            }

            if (request.Cards.Any(x => x == null || x.CardId <= 0 || x.Quantity < 1 || x.Quantity > 4) ||
                request.Cards.Select(x => x.CardId).Distinct().Count() != request.Cards.Count)
                return BadRequest("Chaque carte doit apparaître une seule fois avec une quantité entre 1 et 4.");

            var requestedCards = request.Cards;

            var totalCards =
                requestedCards.Sum(x => x.Quantity);

            if (totalCards > 40)
            {
                return BadRequest(
                    "Un deck ne peut pas contenir plus de 40 cartes.");
            }

            var cardIds =
                requestedCards
                    .Select(x => x.CardId)
                    .ToList();

            var cardEntities =
                await _context.Cards
                    .AsNoTracking()
                    .Where(x => cardIds.Contains(x.Id))
                    .Include(x => x.BaseCard)
                    .ToListAsync();

            if (cardEntities.Count != cardIds.Count)
                return BadRequest("Une carte sélectionnée est introuvable.");

            if (cardEntities.Any(x => SecretCardRules.IsNonPlayable(x) || SecretCardRules.IsNonPlayable(x.BaseCard ?? x)))
                return BadRequest("Les cartes SECRET (numéro 0) sont des objets de collection et ne peuvent pas être jouées.");

            if (cardEntities.Any(x => (x.BaseCard ?? x).Type == CardType.Leader))
            {
                return BadRequest(
                    "Le deck principal ne peut pas contenir de Leader.");
            }

            var groupedByGameplay =
                requestedCards.GroupBy(x =>
                {
                    var card =
                        cardEntities.First(c => c.Id == x.CardId);

                    return card.BaseCardId ?? card.Id;
                });

            foreach (var group in groupedByGameplay)
            {
                if (group.Sum(x => x.Quantity) > 4)
                {
                    return BadRequest(
                        "Une même carte ne peut apparaître que 4 fois dans un deck, variantes comprises.");
                }
            }

            var ownedQuantities =
                await _context.UserCardCollections
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == UserId &&
                        cardIds.Contains(x.CardId))
                    .ToDictionaryAsync(
                        x => x.CardId,
                        x => x.Quantity);

            foreach (var entry in requestedCards)
            {
                if (!ownedQuantities.TryGetValue(
                        entry.CardId,
                        out var owned) ||
                    owned < entry.Quantity)
                {
                    return BadRequest(
                        "Vous ne possédez pas suffisamment d'exemplaires d'une des cartes sélectionnées.");
                }
            }

            DeckEntity? deck = null;

            if (request.Id.HasValue)
            {
                deck =
                    await _context.Set<DeckEntity>()
                        .Include(x => x.Cards)
                        .FirstOrDefaultAsync(x =>
                            x.Id == request.Id.Value &&
                            x.UserId == UserId);

                if (deck == null)
                    return NotFound();
            }

            var nameTaken =
                await _context.Set<DeckEntity>()
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.UserId == UserId &&
                        x.Name == request.Name &&
                        (deck == null || x.Id != deck.Id));

            if (nameTaken)
            {
                return Conflict(
                    "Vous possédez déjà un deck avec ce nom.");
            }

            if (deck == null)
            {
                deck =
                    new DeckEntity
                    {
                        UserId = UserId,
                        Name = request.Name,
                        LeaderCardId = request.LeaderCardId
                    };

                _context.Set<DeckEntity>().Add(
                    deck);
            }
            else
            {
                deck.Name = request.Name;
                deck.LeaderCardId = request.LeaderCardId;
                deck.UpdatedAt = DateTime.UtcNow;

                // Mettre à jour les lignes existantes pour respecter l'index unique
                // (DeckId, CardId) et conserver une seule transaction SaveChanges.
                var quantities = requestedCards.ToDictionary(x => x.CardId, x => x.Quantity);
                foreach (var existing in deck.Cards.ToList())
                {
                    if (quantities.TryGetValue(existing.CardId, out var quantity))
                        existing.Quantity = quantity;
                    else
                    {
                        _context.Set<DeckCard>().Remove(existing);
                        deck.Cards.Remove(existing);
                    }
                }
            }

            foreach (var entry in requestedCards)
            {
                if (deck.Cards.Any(x => x.CardId == entry.CardId))
                    continue;
                DeckCardPersistence.Add(_context, deck, entry.CardId, entry.Quantity);
            }

            await _context.SaveChangesAsync();

            return Json(
                new DeckSummaryDto
                {
                    Id = deck.Id,
                    Name = deck.Name,
                    LeaderName = leaderGameplay.Name,
                    LeaderCardId = deck.LeaderCardId,
                    TotalCards = totalCards,
                    IsComplete = totalCards == 40
                });
        }

        [HttpPost("{id:guid}/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            Guid id)
        {
            var deck =
                await _context.Set<DeckEntity>()
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        x.UserId == UserId);

            if (deck == null)
                return NotFound();

            _context.Set<DeckEntity>().Remove(
                deck);

            await _context.SaveChangesAsync();

            return Ok();
        }

        /// <summary>
        /// Page de gestion des decks de l'utilisateur.
        /// </summary>
        [HttpGet("Manage")]
        public async Task<IActionResult> Manage()
        {
            var userDecks =
                await _context.Set<DeckEntity>()
                    .AsNoTracking()
                    .Where(x => x.UserId == UserId)
                    .Include(x => x.LeaderCard)
                    .Include(x => x.Cards)
                    .OrderBy(x => x.Name)
                    .ToListAsync();

            return View(
                userDecks
                    .Select(x => new DeckSummaryDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        LeaderName = x.LeaderCard?.Name ?? "Aucun leader",
                        LeaderCardId = x.LeaderCardId,
                        TotalCards = x.Cards?.Sum(c => c.Quantity) ?? 0,
                        IsComplete = x.Cards?.Sum(c => c.Quantity) == 40
                    })
                    .ToList());
        }

        [HttpGet("View/{id:guid}")]
        public async Task<IActionResult> Display(Guid id)
        {
            var deck = await _context.Set<DeckEntity>().AsNoTracking().Include(x => x.Cards)
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
            if (deck == null) return NotFound();
            var cards = await new CardPresentationService(_context)
                .GetAsync(deck.Cards.Select(x => x.CardId).Append(deck.LeaderCardId));
            var leader = cards.FirstOrDefault(x => x.Id == deck.LeaderCardId);
            if (leader == null) return NotFound();
            return View("Display", new DeckDisplayViewModel {
                Name = deck.Name, Leader = leader,
                Cards = deck.Cards.Where(x => cards.Any(c => c.Id == x.CardId))
                    .Select(x => (cards.First(c => c.Id == x.CardId), x.Quantity))
                    .OrderBy(x => x.Item1.Name).ToList()
            });
        }
    }
}
