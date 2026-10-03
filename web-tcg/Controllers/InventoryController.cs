using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Models.Card;
using web_tcg.Models.Inventory;
using web_tcg.Services;

namespace web_tcg.Controllers
{
    /// <summary>
    /// Gère l'inventaire des cartes d'un utilisateur.
    ///
    /// Une variante est désormais une Card à part entière :
    /// - carte normale : BaseCardId = null
    /// - variante : BaseCardId = Id de la carte normale
    /// </summary>
    [Authorize]
    public class InventoryController : Controller
    {
        private const decimal DiscardValuePerCard = 1m;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CardBalanceRealtimeService _cardBalanceRealtimeService;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            CardBalanceRealtimeService cardBalanceRealtimeService,
            ILogger<InventoryController> logger)
        {
            _context = context;
            _userManager = userManager;
            _cardBalanceRealtimeService = cardBalanceRealtimeService;
            _logger = logger;
        }

        /// <summary>
        /// Affiche l'inventaire de l'utilisateur.
        /// Chaque variante apparaît comme une carte différente grâce à son CardId.
        /// </summary>
        public async Task<IActionResult> Index(
            string? search = null,
            string? sortBy = "name")
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            try
            {
                // =========================================================
                // REQUÊTE DE BASE
                // =========================================================

                var query = _context.UserCardCollections
                    .AsNoTracking()
                    .Where(x => x.UserId == user.Id)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.Region)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.Country)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.CardSet)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.Abilities)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.BaseCard)
                            .ThenInclude(x => x!.Abilities)
                    .AsQueryable();

                // =========================================================
                // RECHERCHE
                // =========================================================

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var pattern = $"%{search.Trim()}%";

                    query = query.Where(x =>
                        EF.Functions.ILike(x.Card.Name, pattern) ||
                        EF.Functions.ILike(x.Card.Region.Name, pattern) ||
                        EF.Functions.ILike(x.Card.CardSet.Name, pattern) ||
                        (
                            x.Card.Country != null &&
                            EF.Functions.ILike(x.Card.Country.Name, pattern)
                        )
                    );
                }

                // =========================================================
                // TRI
                // =========================================================

                sortBy = string.IsNullOrWhiteSpace(sortBy)
                    ? "name"
                    : sortBy.ToLowerInvariant();

                query = sortBy switch
                {
                    "rarity" => query
                        .OrderByDescending(x => x.Card.Rarity)
                        .ThenBy(x => x.Card.Name),

                    "date" => query
                        .OrderByDescending(x => x.AcquiredDate),

                    "type" => query
                        .OrderBy(x => x.Card.Type)
                        .ThenBy(x => x.Card.Name),

                    "quantity" => query
                        .OrderByDescending(x => x.Quantity)
                        .ThenBy(x => x.Card.Name),

                    _ => query
                        .OrderBy(x => x.Card.Name)
                        .ThenBy(x => x.Card.CollectionNumber)
                        .ThenBy(x => x.Card.VariantNumber)
                };

                // =========================================================
                // CHARGEMENT
                // =========================================================

                var inventoryEntities = await query.ToListAsync();

                /*
                 * Une variante utilise les informations gameplay
                 * de sa carte normale.
                 */
                var gameplayCardIds = inventoryEntities
                    .Select(x => x.Card.BaseCardId ?? x.Card.Id)
                    .Distinct()
                    .ToList();

                // =========================================================
                // POINTS DE VIE
                // =========================================================

                var leaderHealthPoints = await _context.LeaderCards
                    .AsNoTracking()
                    .Where(x => gameplayCardIds.Contains(x.CardId))
                    .ToDictionaryAsync(
                        x => x.CardId,
                        x => x.HealthPoints);

                var characterHealthPoints = await _context.CharacterCards
                    .AsNoTracking()
                    .Where(x => gameplayCardIds.Contains(x.CardId))
                    .ToDictionaryAsync(
                        x => x.CardId,
                        x => x.HealthPoints);

                // =========================================================
                // MAPPING
                // =========================================================

                var inventoryItems = inventoryEntities
                    .Select(item =>
                    {
                        var card = item.Card;

                        /*
                         * Pour une variante :
                         * gameplayCard = carte normale.
                         *
                         * Pour une normale :
                         * gameplayCard = elle-même.
                         */
                        var gameplayCard = card.BaseCard ?? card;
                        var gameplayCardId = card.BaseCardId ?? card.Id;

                        int? healthPoints = null;

                        if (gameplayCard.Type == CardType.Leader &&
                            leaderHealthPoints.TryGetValue(gameplayCardId, out var leaderHp))
                        {
                            healthPoints = leaderHp;
                        }
                        else if (gameplayCard.Type == CardType.Character &&
                                 characterHealthPoints.TryGetValue(gameplayCardId, out var characterHp))
                        {
                            healthPoints = characterHp;
                        }

                        /*
                         * L'image et l'ArtType viennent toujours de la Card
                         * réellement possédée.
                         */
                        var cardViewModel = new CardViewModel
                        {
                            Name = card.Name,
                            Type = card.Type,
                            Rarity = card.Rarity,
                            UsageCost = card.UsageCost,
                            HealthPoints = healthPoints,
                            ImageUrl = card.ImageUrl,
                            ArtType = card.ArtType,
                            CollectionNumber = card.CollectionNumber.ToString(),
                            RegionName = card.Region?.Name ?? "Région",
                            CountryName = card.Country?.Name ?? string.Empty,
                            RegionColor = !string.IsNullOrWhiteSpace(card.Region?.Color)
                                ? card.Region.Color
                                : "#FFD700",
                            CardSetCode = card.CardSet?.Code ?? string.Empty,
                            Abilities = gameplayCard.Abilities
                                .OrderBy(x => x.DisplayOrder)
                                .Select(x => new CardAbilityViewModel
                                {
                                    Type = x.Type,
                                    UsageCost = x.UsageCost,
                                    Name = x.Name,
                                    Description = x.Description,
                                    DisplayOrder = x.DisplayOrder
                                })
                                .ToList()
                        };

                        return new InventoryCardViewModel
                        {
                            Id = item.Id,
                            Quantity = item.Quantity,
                            AcquiredDate = item.AcquiredDate,
                            Card = cardViewModel
                        };
                    })
                    .ToList();

                // =========================================================
                // VIEWMODEL FINAL
                // =========================================================

                var viewModel = new InventoryViewModel
                {
                    Items = inventoryItems,
                    TotalCards = inventoryEntities.Sum(x => x.Quantity),
                    UniqueCards = inventoryEntities.Count,
                    SearchQuery = search,
                    CurrentSort = sortBy
                };

                _logger.LogInformation(
                    "Inventaire affiché pour {UserId} : {UniqueCards} cartes uniques, {TotalCards} cartes au total.",
                    user.Id,
                    viewModel.UniqueCards,
                    viewModel.TotalCards);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de la récupération de l'inventaire pour {UserId}.",
                    user.Id);

                return BadRequest(
                    "Une erreur est survenue lors du chargement de votre inventaire.");
            }
        }

        /// <summary>
        /// Affiche les détails d'une carte précise de l'inventaire.
        /// </summary>
        public async Task<IActionResult> Detail(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            try
            {
                var inventoryItem = await _context.UserCardCollections
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == user.Id &&
                        x.Id == id)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.Region)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.Country)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.CardSet)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.Abilities)
                    .Include(x => x.Card)
                        .ThenInclude(x => x.BaseCard)
                            .ThenInclude(x => x!.Abilities)
                    .FirstOrDefaultAsync();

                if (inventoryItem == null)
                    return NotFound("Carte non trouvée dans votre inventaire.");

                var card = inventoryItem.Card;
                var gameplayCard = card.BaseCard ?? card;
                var gameplayCardId = card.BaseCardId ?? card.Id;

                // =========================================================
                // POINTS DE VIE
                // =========================================================

                int? healthPoints = null;

                if (gameplayCard.Type == CardType.Leader)
                {
                    healthPoints = await _context.LeaderCards
                        .AsNoTracking()
                        .Where(x => x.CardId == gameplayCardId)
                        .Select(x => (int?)x.HealthPoints)
                        .FirstOrDefaultAsync();
                }
                else if (gameplayCard.Type == CardType.Character)
                {
                    healthPoints = await _context.CharacterCards
                        .AsNoTracking()
                        .Where(x => x.CardId == gameplayCardId)
                        .Select(x => (int?)x.HealthPoints)
                        .FirstOrDefaultAsync();
                }

                // =========================================================
                // CARD VIEWMODEL
                // =========================================================

                var cardViewModel = new CardViewModel
                {
                    Name = card.Name,
                    Type = card.Type,
                    Rarity = card.Rarity,
                    UsageCost = card.UsageCost,
                    HealthPoints = healthPoints,
                    ImageUrl = card.ImageUrl,
                    ArtType = card.ArtType,
                    CollectionNumber = card.CollectionNumber.ToString(),
                    RegionName = card.Region?.Name ?? "Région",
                    RegionColor = !string.IsNullOrWhiteSpace(card.Region?.Color)
                        ? card.Region.Color
                        : "#FFD700",
                    CountryName = card.Country?.Name ?? string.Empty,
                    CardSetCode = card.CardSet?.Code ?? string.Empty,
                    Abilities = gameplayCard.Abilities
                        .OrderBy(x => x.DisplayOrder)
                        .Select(x => new CardAbilityViewModel
                        {
                            Type = x.Type,
                            Name = x.Name,
                            Description = x.Description,
                            UsageCost = x.UsageCost,
                            DisplayOrder = x.DisplayOrder
                        })
                        .ToList()
                };

                // =========================================================
                // DETAIL VIEWMODEL
                // =========================================================

                var viewModel = new InventoryDetailViewModel
                {
                    Id = inventoryItem.Id,
                    CardId = card.Id,
                    Quantity = inventoryItem.Quantity,
                    AcquiredDate = inventoryItem.AcquiredDate,
                    Condition = inventoryItem.Condition,
                    Notes = inventoryItem.Notes,
                    IsForSale = inventoryItem.IsForSale,
                    SalePrice = inventoryItem.SalePrice,
                    CardSetName = card.CardSet?.Name ?? string.Empty,
                    CardSetReleaseDate = card.CardSet?.ReleaseDate ?? DateTime.MinValue,
                    VariantNumber = card.VariantNumber,
                    Card = cardViewModel
                };

                _logger.LogInformation(
                    "Détails de la carte {CardId} affichés pour {UserId}",
                    card.Id,
                    user.Id);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de la récupération des détails de la carte d'inventaire {InventoryId}",
                    id);

                return BadRequest("Une erreur est survenue.");
            }
        }

        /// <summary>
        /// Ajoute une carte précise à l'inventaire.
        /// Une variante possède son propre CardId.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddCard(
            int cardId,
            int quantity = 1)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            if (quantity <= 0)
                return BadRequest("La quantité doit être supérieure à zéro.");

            try
            {
                var card = await _context.Cards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == cardId);

                if (card == null)
                    return BadRequest("Carte non trouvée.");

                if (!card.IsActive)
                    return BadRequest("Cette carte n'est plus disponible.");

                var existingItem = await _context.UserCardCollections
                    .FirstOrDefaultAsync(x =>
                        x.UserId == user.Id &&
                        x.CardId == cardId);

                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    var newItem = new UserCardCollection
                    {
                        UserId = user.Id,
                        CardId = cardId,
                        Quantity = quantity,
                        AcquiredDate = DateTime.UtcNow
                    };

                    _context.UserCardCollections.Add(newItem);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Carte {CardId} ajoutée à l'inventaire de {UserId} (quantité : {Quantity}).",
                    cardId,
                    user.Id,
                    quantity);

                return Json(new
                {
                    success = true,
                    message = "Carte ajoutée à votre inventaire."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de l'ajout de la carte {CardId} pour {UserId}.",
                    cardId,
                    user.Id);

                return BadRequest(
                    "Une erreur est survenue lors de l'ajout de la carte.");
            }
        }

        /// <summary>
        /// Défausse une quantité d'une carte possédée.
        /// Chaque carte rapporte 1 unité de CardBalance,
        /// quelle que soit sa rareté.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DiscardCard(
            int id,
            int quantity)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (quantity <= 0)
            {
                TempData["InventoryError"] =
                    "La quantité à défausser doit être supérieure à zéro.";

                return RedirectToAction(
                    nameof(Detail),
                    new { id });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var inventoryItem = await _context.UserCardCollections
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        x.UserId == userId);

                if (inventoryItem == null)
                {
                    await transaction.RollbackAsync();

                    return NotFound(
                        "Carte non trouvée dans votre inventaire.");
                }

                if (quantity > inventoryItem.Quantity)
                {
                    await transaction.RollbackAsync();

                    TempData["InventoryError"] =
                        $"Vous ne possédez que {inventoryItem.Quantity} exemplaire(s) de cette carte.";

                    return RedirectToAction(
                        nameof(Detail),
                        new { id });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(x => x.Id == userId);

                if (user == null)
                {
                    await transaction.RollbackAsync();
                    return Unauthorized();
                }

                var cardId = inventoryItem.CardId;
                var ownedQuantityBeforeDiscard = inventoryItem.Quantity;

                var discardAll =
                    quantity == ownedQuantityBeforeDiscard;

                var reward =
                    quantity * DiscardValuePerCard;

                if (discardAll)
                {
                    /*
                     * Si la dernière copie est défaussée,
                     * elle ne peut plus rester dans la vitrine.
                     */
                    var showcaseEntries =
                        await _context.UserProfileShowcaseCards
                            .Where(x =>
                                x.UserId == userId &&
                                x.CardId == cardId)
                            .ToListAsync();

                    if (showcaseEntries.Count > 0)
                    {
                        _context.UserProfileShowcaseCards
                            .RemoveRange(showcaseEntries);
                    }

                    _context.UserCardCollections.Remove(
                        inventoryItem);
                }
                else
                {
                    inventoryItem.Quantity -= quantity;
                }

                user.CardBalance += reward;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                /*
                 * SignalR est volontairement exécuté après le commit :
                 * si SignalR échoue, la défausse reste validée.
                 */
                try
                {
                    await _cardBalanceRealtimeService.PublishAsync(
                        user.Id,
                        user.CardBalance);
                }
                catch (Exception signalRException)
                {
                    _logger.LogWarning(
                        signalRException,
                        "Défausse validée mais mise à jour SignalR de CardBalance impossible pour {UserId}.",
                        user.Id);
                }

                _logger.LogInformation(
                    "Utilisateur {UserId} a défaussé {Quantity} exemplaire(s) de la carte {CardId} pour {Reward} CardBalance. Quantité précédente : {PreviousQuantity}.",
                    user.Id,
                    quantity,
                    cardId,
                    reward,
                    ownedQuantityBeforeDiscard);

                TempData["InventorySuccess"] =
                    $"{quantity} carte(s) défaussée(s) pour {reward:N0}.";

                if (discardAll)
                {
                    return RedirectToAction(
                        nameof(Index));
                }

                return RedirectToAction(
                    nameof(Detail),
                    new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Erreur lors de la défausse de {Quantity} exemplaire(s) de l'entrée d'inventaire {InventoryId} pour {UserId}.",
                    quantity,
                    id,
                    userId);

                TempData["InventoryError"] =
                    "Une erreur est survenue pendant la défausse de la carte.";

                return RedirectToAction(
                    nameof(Detail),
                    new { id });
            }
        }

        /// <summary>
        /// Met à jour la quantité possédée d'une carte.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(
            int id,
            int quantity)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            if (quantity <= 0)
                return BadRequest("La quantité doit être positive.");

            try
            {
                var inventoryItem = await _context.UserCardCollections
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        x.UserId == user.Id);

                if (inventoryItem == null)
                    return NotFound("Carte non trouvée.");

                inventoryItem.Quantity = quantity;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Quantité de la carte {CardId} mise à jour pour {UserId} : {Quantity}.",
                    inventoryItem.CardId,
                    user.Id,
                    quantity);

                return Json(new
                {
                    success = true,
                    message = "Quantité mise à jour."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de la mise à jour de la quantité de l'entrée {InventoryId}.",
                    id);

                return BadRequest("Une erreur est survenue.");
            }
        }
    }
}