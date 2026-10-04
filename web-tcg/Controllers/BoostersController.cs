using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game;
using web_tcg.Models.Booster;
using web_tcg.Models.BoosterShop;
using web_tcg.Models.Card;
using web_tcg.Services;

namespace web_tcg.Controllers
{
    [Authorize]
    public class BoosterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BoosterGenerator _boosterGenerator;
        private readonly BoosterCardSelectionService _cardSelectionService;
        private readonly CardCollectionService _collectionService;

        private static readonly TimeSpan OpeningAccessLifetime = TimeSpan.FromMinutes(1);

        private const string CurrentOpeningTempDataKey = "CurrentBoosterOpeningId";

        public BoosterController(
            ApplicationDbContext context,
            BoosterGenerator boosterGenerator,
            BoosterCardSelectionService cardSelectionService,
            CardCollectionService collectionService)
        {
            _context = context;
            _boosterGenerator = boosterGenerator;
            _cardSelectionService = cardSelectionService;
            _collectionService = collectionService;
        }

        /// <summary>
        /// Affiche la liste des extensions disponibles.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cardSets = await _context.CardSets
                .AsNoTracking()
                .OrderByDescending(x => x.ReleaseDate)
                .ToListAsync();

            var viewModel = new BoosterShopViewModel
            {
                CardSets = cardSets
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> DropRates(int id)
        {
            var cardSet = await _context.CardSets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (cardSet == null) return NotFound();

            var cards = await _context.Cards.AsNoTracking()
                .Where(x => x.CardSetId == id && x.IsActive).ToListAsync();
            var presentation = (await new CardPresentationService(_context).GetAsync(cards.Select(x => x.Id)))
                .ToDictionary(x => x.Id);
            var rates = _boosterGenerator.GetDropRates(5);
            var model = new BoosterDropRatesViewModel
            {
                CardSetName = cardSet.Name,
                Groups = rates.OrderByDescending(x => x.Key).Select(rate => new BoosterDropGroup
                {
                    Rarity = rate.Key,
                    Rate = rate.Value,
                    Cards = BoosterCardSelectionService.FilterCards(cards.AsQueryable(), rate.Key)
                        .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => presentation[x.Id]).ToList()
                }).ToList()
            };
            return PartialView("~/Views/Booster/_DropRates.cshtml", model);
        }

        /// <summary>
        /// Achète et génère un nouveau booster.
        /// L'identifiant de l'ouverture n'est jamais envoyé dans l'URL.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var cardSet = await _context.CardSets
                .FirstOrDefaultAsync(x => x.Id == id);

            if (cardSet == null)
                return NotFound();

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(x => x.Id == userId);

                if (user == null)
                {
                    await transaction.RollbackAsync();
                    return Unauthorized();
                }

                var boosterPrice = cardSet.BoosterPrice;

                if (user.Balance < boosterPrice)
                {
                    TempData["BoosterError"] =
                        "Vous n'avez pas assez de monnaie.";

                    await transaction.RollbackAsync();

                    return RedirectToAction(nameof(Index));
                }

                user.Balance -= boosterPrice;

                var boosterResult =
                    _boosterGenerator.Generate(5);

                var opening = new BoosterOpening
                {
                    UserId = userId,
                    PricePaid = boosterPrice,
                    OpenedAt = DateTime.UtcNow
                };

                _context.BoosterOpenings.Add(opening);

                foreach (var result in boosterResult.Cards)
                {
                    var card = await _cardSelectionService
                        .SelectCardAsync(
                            result.Rarity,
                            cardSet.Id);

                    opening.Cards.Add(
                        new BoosterOpeningCard
                        {
                            Position = result.Position,
                            CardId = card.Id,
                            Rarity = result.Rarity.ToString(),
                            IsRevealed = false
                        });

                    await _collectionService.AddCardAsync(
                        userId,
                        card.Id);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                /*
                 * L'identifiant est conservé temporairement dans TempData.
                 *
                 * Le fournisseur TempData par défaut d'ASP.NET Core
                 * protège les données côté cookie.
                 *
                 * L'ID n'apparaît donc plus dans l'URL.
                 */
                TempData[CurrentOpeningTempDataKey] =
                    opening.Id;

                return RedirectToAction(
                    nameof(OpenResult));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Affiche l'ouverture active.
        ///
        /// URL :
        /// /Booster/OpenResult
        ///
        /// Aucun ID d'ouverture n'est accepté dans l'URL.
        /// </summary>
        [HttpGet]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public async Task<IActionResult> OpenResult()
        {
            Response.Headers["Cache-Control"] =
                "no-store, no-cache, must-revalidate, max-age=0";

            Response.Headers["Pragma"] =
                "no-cache";

            Response.Headers["Expires"] =
                "0";

            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            /*
             * Peek permet de lire la valeur sans la supprimer.
             * Le refresh reste donc possible pendant la minute autorisée.
             */
            var openingValue =
                TempData.Peek(
                    CurrentOpeningTempDataKey);

            if (openingValue == null)
            {
                TempData["BoosterError"] =
                    "Aucune ouverture de booster active.";

                return RedirectToAction(
                    nameof(Index));
            }

            if (!int.TryParse(
                    openingValue.ToString(),
                    out var openingId))
            {
                TempData.Remove(
                    CurrentOpeningTempDataKey);

                TempData["BoosterError"] =
                    "L'ouverture du booster n'est plus disponible.";

                return RedirectToAction(
                    nameof(Index));
            }

            var opening = await _context.BoosterOpenings
                .AsNoTracking()
                .Include(x => x.Cards)
                    .ThenInclude(x => x.Card)
                        .ThenInclude(c => c.CardSet)
                .Include(x => x.Cards)
                    .ThenInclude(x => x.Card)
                        .ThenInclude(c => c.Region)
                .Include(x => x.Cards)
                    .ThenInclude(x => x.Card)
                        .ThenInclude(c => c.Country)
                .Include(x => x.Cards)
                    .ThenInclude(x => x.Card)
                        .ThenInclude(c => c.Abilities)
                .Include(x => x.Cards)
                    .ThenInclude(x => x.Card)
                        .ThenInclude(c => c.BaseCard)
                            .ThenInclude(c => c!.Abilities)
                .FirstOrDefaultAsync(x =>
                    x.Id == openingId &&
                    x.UserId == userId);

            /*
             * Même si quelqu'un modifie ses cookies ou essaye
             * d'accéder directement à la route, on vérifie toujours
             * que l'ouverture appartient au compte connecté.
             */
            if (opening == null)
            {
                TempData.Remove(
                    CurrentOpeningTempDataKey);

                TempData["BoosterError"] =
                    "Cette ouverture n'est plus disponible.";

                return RedirectToAction(
                    nameof(Index));
            }

            var expiresAt =
                opening.OpenedAt
                    .Add(OpeningAccessLifetime);

            if (DateTime.UtcNow >= expiresAt)
            {
                TempData.Remove(
                    CurrentOpeningTempDataKey);

                TempData["BoosterError"] =
                    "Cette ouverture a expiré. Un booster ne peut être consulté que pendant la minute suivant son ouverture.";

                return RedirectToAction(
                    nameof(Index));
            }

            var gameplayCardIds = opening.Cards
                .Select(x =>
                    x.Card.BaseCardId ??
                    x.Card.Id)
                .Distinct()
                .ToList();

            var leaderHealthPoints =
                await _context.LeaderCards
                    .AsNoTracking()
                    .Where(x =>
                        gameplayCardIds.Contains(
                            x.CardId))
                    .ToDictionaryAsync(
                        x => x.CardId,
                        x => x.HealthPoints);

            var characterHealthPoints =
                await _context.CharacterCards
                    .AsNoTracking()
                    .Where(x =>
                        gameplayCardIds.Contains(
                            x.CardId))
                    .ToDictionaryAsync(
                        x => x.CardId,
                        x => x.HealthPoints);

            var cards = opening.Cards
                .OrderBy(x => x.Position)
                .Select(x =>
                {
                    var physicalCard =
                        x.Card;

                    var gameplayCard =
                        physicalCard.BaseCard ??
                        physicalCard;

                    var gameplayCardId =
                        physicalCard.BaseCardId ??
                        physicalCard.Id;

                    int? healthPoints = null;

                    if (
                        gameplayCard.Type ==
                        CardType.Leader &&
                        leaderHealthPoints.TryGetValue(
                            gameplayCardId,
                            out var leaderHp))
                    {
                        healthPoints =
                            leaderHp;
                    }
                    else if (
                        gameplayCard.Type ==
                        CardType.Character &&
                        characterHealthPoints.TryGetValue(
                            gameplayCardId,
                            out var characterHp))
                    {
                        healthPoints =
                            characterHp;
                    }

                    var cardViewModel =
                        new CardViewModel
                        {
                            Id =
                                physicalCard.Id,

                            Name =
                                physicalCard.Name,

                            Type =
                                physicalCard.Type,

                            Rarity =
                                physicalCard.Rarity,

                            UsageCost =
                                physicalCard.UsageCost,

                            HealthPoints =
                                healthPoints,

                            Abilities =
                                gameplayCard.Abilities
                                    .OrderBy(
                                        ability =>
                                            ability.DisplayOrder)
                                    .Select(
                                        ability =>
                                            new CardAbilityViewModel
                                            {
                                                Type =
                                                    ability.Type,

                                                Name =
                                                    ability.Name,

                                                Description =
                                                    ability.Description,

                                                UsageCost =
                                                    ability.UsageCost,

                                                DisplayOrder =
                                                    ability.DisplayOrder
                                            })
                                    .ToList(),

                            ImageUrl =
                                physicalCard.ImageUrl,

                            ArtType =
                                physicalCard.ArtType,

                            CollectionNumber =
                                physicalCard.CollectionNumber
                                    .ToString(),

                            CardSetCode =
                                physicalCard.CardSet?.Code ??
                                string.Empty,

                            RegionName =
                                physicalCard.Region?.Name ??
                                string.Empty,

                            CountryName =
                                physicalCard.Country?.Name ??
                                string.Empty,

                            RegionColor =
                                physicalCard.Region?.Color ??
                                "#FFD700"
                        };

                    return new BoosterOpeningCardViewModel
                    {
                        Position =
                            x.Position,

                        CardId =
                            physicalCard.Id,

                        Rarity =
                            x.Rarity,

                        IsRevealed =
                            x.IsRevealed,

                        Card =
                            cardViewModel
                    };
                })
                .ToList();

            var model =
                new BoosterOpeningViewModel
                {
                    /*
                     * On peut garder OpeningId dans le modèle C#
                     * si tu en as besoin côté serveur.
                     *
                     * On ne l'affichera simplement plus dans le HTML.
                     */
                    OpeningId =
                        opening.Id,

                    CardSetId =
                        opening.Cards
                            .Select(x =>
                                x.Card.CardSetId)
                            .FirstOrDefault(),

                    OpenedAt =
                        opening.OpenedAt,

                    Cards =
                        cards
                };

            return View(model);
        }
    }
}
