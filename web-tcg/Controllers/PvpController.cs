using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Game.Engine;
using web_tcg.Models.Home;
using web_tcg.Services;

namespace web_tcg.Controllers
{
    [Authorize]
    [Route("Pvp")]
    public sealed class PvpController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GameService _gameService;
        private readonly DeckValidationService _deckValidation;
        private readonly MatchmakingService _matchmaking;

        public PvpController(
            ApplicationDbContext context,
            GameService gameService,
            DeckValidationService deckValidation,
            MatchmakingService matchmaking)
        {
            _context = context;
            _gameService = gameService;
            _deckValidation = deckValidation;
            _matchmaking = matchmaking;
        }

        private string UserId =>
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException(
                "Utilisateur non authentifié.");

        [HttpGet("Activity")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Activity(CancellationToken cancellationToken)
        {
            var activeMatches = _context.Set<GameMatch>().AsNoTracking()
                .Where(match => match.Status == web_tcg.Data.Enums.GameMatchStatus.InProgress);
            var players = activeMatches.Select(match => match.PlayerOneId)
                .Concat(activeMatches.Select(match => match.PlayerTwoId)).Distinct();
            var playersInGame = await players.CountAsync(id => _context.Users.Any(user => user.Id == id && !user.IsBot), cancellationToken);
            return Json(new { playersInGame, playersInQueue = _matchmaking.QueueCount });
        }

        [HttpGet("Play")]
        public async Task<IActionResult> Play()
        {
            var userDecks =
                await _context.Set<Deck>()
                    .AsNoTracking()
                    .Where(x => x.UserId == UserId)
                    .Select(x => new HomeDeckOption
                    {
                        Id = x.Id,
                        Name = x.Name
                    })
                    .OrderBy(x => x.Name)
                    .ToListAsync();

            var model = new HomeViewModel
            {
                UserDecks = userDecks
            };

            return View(model);
        }

        /// <summary>
        /// Lance immédiatement une partie contre l'IA avec le deck sélectionné.
        /// </summary>
        [HttpPost("PlayVsIA")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlayVsIA(
            [FromBody] PlayVsIaRequest request)
        {
            if (!ModelState.IsValid || request == null || request.DeckId == Guid.Empty)
                return BadRequest("Veuillez sélectionner un deck valide.");
            try
            {
                await _deckValidation.ValidateAsync(request.DeckId, UserId);
                var botUser =
                    await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.IsBot);

                if (botUser == null)
                {
                    return BadRequest(
                        "Aucun bot IA disponible.");
                }

                var botDeck =
                    await _context.Set<Deck>()
                        .AsNoTracking()
                        .Where(x => x.UserId == botUser.Id)
                        .FirstOrDefaultAsync();

                if (botDeck == null)
                {
                    return BadRequest(
                        "Le bot n'a pas de deck configuré.");
                }

                var matchId =
                    await _gameService.CreateMatchAsync(
                        UserId,
                        request.DeckId,
                        botUser.Id,
                        botDeck.Id);

                return Json(new { matchId = matchId });
            }
            catch (GameRuleException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(
                    500,
                    "Erreur lors du lancement de la partie.");
            }
        }

        public sealed class PlayVsIaRequest
        {
            public Guid DeckId { get; set; }
        }
    }
}

