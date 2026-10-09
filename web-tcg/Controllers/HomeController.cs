using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Models;
using web_tcg.Models.Home;
using web_tcg.Services;

namespace web_tcg.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GameService _gameService;

        public HomeController(
            ApplicationDbContext context,
            GameService gameService)
        {
            _context = context;
            _gameService = gameService;
        }

        public async Task<IActionResult> Index()
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var now = DateTime.UtcNow;
            var releasedSets = _context.CardSets.AsNoTracking()
                .Where(x => x.ReleaseDate <= now);
            var catalogCards = _context.Cards.AsNoTracking()
                .Where(x => x.IsActive && x.CardSet.ReleaseDate <= now);

            var availableCards =
                await catalogCards.CountAsync();

            var baseCards =
                await catalogCards
                    .CountAsync(x =>
                        x.BaseCardId == null);

            var variants =
                await catalogCards
                    .CountAsync(x =>
                        x.BaseCardId != null);

            var registeredPlayers =
                await _context.Users
                    .AsNoTracking()
                    .CountAsync();

            var cardSets =
                await releasedSets.CountAsync();

            var boosterOpenings =
                await _context.BoosterOpenings
                    .AsNoTracking()
                    .CountAsync();

            var typeCounts =
                await catalogCards
                    .Where(x =>
                        x.BaseCardId == null)
                    .GroupBy(x => x.Type)
                    .Select(x => new
                    {
                        Type = x.Key,
                        Count = x.Count()
                    })
                    .ToDictionaryAsync(
                        x => x.Type,
                        x => x.Count);

            var userUniqueCards = 0;
            var userTotalCards = 0;
            var userDecks =
                new List<HomeDeckOption>();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                // Use the same catalogue for the collection numerator and denominator.
                var catalogCollection = _context.UserCardCollections.AsNoTracking()
                    .Where(x => x.UserId == userId && x.Quantity > 0
                        && catalogCards.Select(card => card.Id).Contains(x.CardId));
                userUniqueCards =
                    await catalogCollection.CountAsync();

                userTotalCards =
                    await catalogCollection
                        .SumAsync(
                            x => (int?)x.Quantity)
                    ?? 0;

                userDecks =
                    await _context.Set<Deck>()
                        .AsNoTracking()
                        .Where(x => x.UserId == userId)
                        .OrderBy(x => x.Name)
                        .Select(x => new HomeDeckOption
                        {
                            Id = x.Id,
                            Name = x.Name
                        })
                        .ToListAsync();
            }

            var collectionProgress =
                availableCards > 0
                    ? Math.Round(
                        (double)userUniqueCards /
                        availableCards *
                        100,
                        1)
                    : 0;

            var model =
                new HomeViewModel
                {
                    AvailableCards =
                        availableCards,

                    BaseCards =
                        baseCards,

                    Variants =
                        variants,

                    RegisteredPlayers =
                        registeredPlayers,

                    CardSets =
                        cardSets,

                    BoosterOpenings =
                        boosterOpenings,

                    LeaderCards =
                        typeCounts.GetValueOrDefault(
                            CardType.Leader),

                    CharacterCards =
                        typeCounts.GetValueOrDefault(
                            CardType.Character),

                    PoliticalDecisionCards =
                        typeCounts.GetValueOrDefault(
                            CardType.PoliticalDecision),

                    LocationCards =
                        typeCounts.GetValueOrDefault(
                            CardType.Location),

                    UserUniqueCards =
                        userUniqueCards,

                    UserTotalCards =
                        userTotalCards,

                    CollectionProgress =
                        collectionProgress,

                    UserDecks =
                        userDecks
                };

            return View(model);
        }

        /// <summary>
        /// Lance immédiatement une partie contre le bot IA
        /// avec le deck sélectionné par le joueur.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlayAi(
            Guid deckId)
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var botUser =
                await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.IsBot);

            if (botUser == null)
            {
                return BadRequest(
                    "Aucun bot IA n'est disponible pour le moment.");
            }

            var botDeckId =
                await _context.Set<Deck>()
                    .AsNoTracking()
                    .Where(x => x.UserId == botUser.Id)
                    .Select(x => (Guid?)x.Id)
                    .FirstOrDefaultAsync();

            if (botDeckId == null)
            {
                return BadRequest(
                    "Le bot IA ne possède pas de deck configuré.");
            }

            try
            {
                var matchId =
                    await _gameService.CreateMatchAsync(
                        userId,
                        deckId,
                        botUser.Id,
                        botDeckId.Value);

                return RedirectToAction(
                    "Play",
                    "Game",
                    new
                    {
                        id = matchId
                    });
            }
            catch (GameRuleException ex)
            {
                return BadRequest(
                    ex.Message);
            }
        }

        /// <summary>
        /// Politique de confidentialité.
        /// Accessible même sans authentification.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Conditions générales d'utilisation.
        /// Accessible même sans authentification.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Terms()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return View(
                new ErrorViewModel
                {
                    RequestId =
                        Activity.Current?.Id ??
                        HttpContext.TraceIdentifier
                });
        }
    }
}
