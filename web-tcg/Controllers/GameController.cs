using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Game.Engine;
using web_tcg.Models.Game;
using web_tcg.Services;

namespace web_tcg.Controllers
{
    [Authorize]
    [Route("Game")]
    public sealed class GameController :
        Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GameService _gameService;
        private readonly GameStateRepository _repository;
        private readonly GameStateProjector _projector;
        private readonly CardPresentationService _presentation;

        public GameController(
            ApplicationDbContext context,
            GameService gameService,
            GameStateRepository repository,
            GameStateProjector projector,
            CardPresentationService presentation)
        {
            _context =
                context;

            _gameService =
                gameService;
            _repository = repository;
            _projector = projector;
            _presentation = presentation;
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Play(
            Guid id)
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            try
            {
                await _gameService
                    .EnsurePlayerInMatchAsync(
                        id,
                        userId);

                return View(
                    new GameViewModel
                    {
                        MatchId = id,
                        VisibleCards = await GetVisibleCardsAsync(id, userId)
                    });
            }
            catch (GameRuleException)
            {
                return NotFound();
            }
        }

        private async Task<IReadOnlyList<web_tcg.Models.Card.CardViewModel>> GetVisibleCardsAsync(Guid id, string userId)
        {
            var state = await _repository.LoadAsync(id);
            if (state == null) return Array.Empty<web_tcg.Models.Card.CardViewModel>();
            var projected = _projector.Project(state, userId);
            var cards = projected.Me.Hand.Concat(projected.Me.Characters.OfType<web_tcg.Models.Game.GameCardDto>())
                .Concat(projected.Opponent.Characters.OfType<web_tcg.Models.Game.GameCardDto>())
                .Append(projected.Me.Leader).Append(projected.Opponent.Leader).ToList();
            if (projected.Me.Location != null) cards.Add(projected.Me.Location);
            if (projected.Opponent.Location != null) cards.Add(projected.Opponent.Location);
            return await _presentation.GetAsync(cards.Select(c => c.CardId));
        }

        /// <summary>
        /// Route temporaire permettant de tester le moteur
        /// avant la mise en place du matchmaking.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("CreateTestMatch")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTestMatch(
            CreateGameRequest request)
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var opponentExists =
                await _context.Users
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.Id ==
                            request.OpponentUserId);

            if (!opponentExists)
            {
                return BadRequest(
                    "Adversaire introuvable.");
            }

            try
            {
                var matchId =
                    await _gameService.CreateMatchAsync(
                        userId,
                        request.MyDeckId,
                        request.OpponentUserId,
                        request.OpponentDeckId);

                return RedirectToAction(
                    nameof(Play),
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
    }
}
