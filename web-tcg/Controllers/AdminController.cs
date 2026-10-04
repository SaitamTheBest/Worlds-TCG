using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Models.Admin;
using web_tcg.Models.Card;
using web_tcg.Services;

namespace web_tcg.Controllers
{
    /// <summary>
    /// Contrôleur de gestion de l'administration du TCG.
    /// Toutes les actions sont réservées aux utilisateurs ayant le rôle Admin.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ============================================================
        // CARTES
        // ============================================================

        /// <summary>
        /// Affiche toutes les cartes disponibles dans le TCG.
        /// Les variantes sont affichées comme des cartes distinctes
        /// tout en partageant les données gameplay de la carte principale.
        /// </summary>
        [HttpGet("Cards")]
        public async Task<IActionResult> Cards(int? cardSetId)
        {
            var query = _context.Cards
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Include(x => x.CardSet)
                .Include(x => x.Region)
                .Include(x => x.Country)
                .Include(x => x.Abilities).ThenInclude(a => a.Effects)
                .Include(x => x.Abilities).ThenInclude(a => a.Conditions)
                .Include(x => x.BaseCard)
                    .ThenInclude(x => x!.Abilities)
                .AsQueryable();

            if (cardSetId.HasValue)
            {
                query = query.Where(x =>
                    x.CardSetId == cardSetId.Value);
            }

            var cards = await query
                .OrderBy(x => x.CardSet.Code)
                .ThenBy(x => x.CollectionNumber)
                .ThenBy(x => x.VariantNumber ?? 0)
                .ToListAsync();

            var gameplayCardIds = cards
                .Select(x => x.BaseCardId ?? x.Id)
                .Distinct()
                .ToList();

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

            var items = cards.Select(card =>
            {
                var gameplayCard = card.BaseCard ?? card;
                var gameplayCardId = card.BaseCardId ?? card.Id;

                int? healthPoints = null;

                if (gameplayCard.Type == CardType.Leader &&
                    leaderHealthPoints.TryGetValue(
                        gameplayCardId,
                        out var leaderHp))
                {
                    healthPoints = leaderHp;
                }
                else if (
                    gameplayCard.Type == CardType.Character &&
                    characterHealthPoints.TryGetValue(
                        gameplayCardId,
                        out var characterHp))
                {
                    healthPoints = characterHp;
                }

                var cardViewModel = new CardViewModel
                {
                    Name = card.Name,
                    Type = card.Type,
                    Rarity = card.Rarity,
                    UsageCost = card.UsageCost,
                    HealthPoints = healthPoints,

                    // Chaque Card porte directement sa propre image.
                    ImageUrl = card.ImageUrl,

                    // Normal / FullArt / AlternateArt
                    ArtType = card.ArtType,

                    CollectionNumber =
                        card.CollectionNumber.ToString(),

                    RegionName =
                        card.Region?.Name ?? "Région",

                    CountryName =
                        card.Country?.Name ?? string.Empty,

                    RegionColor =
                        !string.IsNullOrWhiteSpace(card.Region?.Color)
                            ? card.Region.Color
                            : "#FFD700",

                    CardSetCode =
                        card.CardSet?.Code ?? string.Empty,

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

                return new AdminCardListItemViewModel
                {
                    /*
                     * ID de la vraie Card affichée.
                     *
                     * Normal :
                     * CardId = 15
                     *
                     * Full Art :
                     * CardId = 16
                     */
                    CardId = card.Id,

                    /*
                     * Tu peux garder cette propriété pour ta vue,
                     * mais elle représente maintenant également un Card.Id.
                     */
                    VariantId = card.BaseCardId.HasValue
                        ? card.Id
                        : null,

                    VariantNumber = card.VariantNumber,

                    Name = card.Name,
                    Type = card.Type,
                    Rarity = card.Rarity,
                    ArtType = card.ArtType,

                    CollectionNumber =
                        card.CollectionNumber,

                    CardSetId =
                        card.CardSetId,

                    CardSetCode =
                        card.CardSet?.Code ?? string.Empty,

                    CardSetName =
                        card.CardSet?.Name ?? string.Empty,

                    RegionName =
                        card.Region?.Name ?? string.Empty,

                    RegionColor =
                        !string.IsNullOrWhiteSpace(card.Region?.Color)
                            ? card.Region.Color
                            : "#FFD700",

                    Card = cardViewModel
                };
            }).ToList();

            ViewBag.CardSetId = cardSetId;

            return View(
                "~/Views/Admin/Cards/Index.cshtml",
                items);
        }

        /// <summary>
        /// Affiche le formulaire de création ou de modification d'une carte.
        ///
        /// Sans id : création.
        /// Avec l'id d'une carte normale : modification.
        /// Avec l'id d'une variante : modification de sa carte principale.
        /// </summary>
        [HttpGet("Cards/Create/{id:int?}")]
        public async Task<IActionResult> CreateCard(int? id = null)
        {
            await LoadReferenceData();

            // ============================================================
            // MODE CRÉATION
            // ============================================================

            if (!id.HasValue)
            {
                return View(
                    "~/Views/Admin/Cards/Create.cshtml",
                    new CreateCardViewModel());
            }

            // ============================================================
            // IDENTIFICATION DE LA CARTE PRINCIPALE
            // ============================================================

            var requestedCard = await _context.Cards
                .AsNoTracking()
                .Where(x => x.Id == id.Value)
                .Select(x => new
                {
                    x.Id,
                    x.BaseCardId
                })
                .FirstOrDefaultAsync();

            if (requestedCard == null)
            {
                return NotFound();
            }

            /*
             * Si l'id correspond à une variante :
             * on ouvre sa carte principale.
             *
             * Sinon l'id correspond déjà à la carte principale.
             */
            var baseCardId =
                requestedCard.BaseCardId
                ?? requestedCard.Id;

            // ============================================================
            // CHARGEMENT DE LA CARTE PRINCIPALE
            // ============================================================

            var card = await _context.Cards
                .AsNoTracking()
                .Include(x => x.Variants)
                .Include(x => x.Abilities).ThenInclude(a => a.Effects)
                .Include(x => x.Abilities).ThenInclude(a => a.Conditions)
                .FirstOrDefaultAsync(x =>
                    x.Id == baseCardId &&
                    x.BaseCardId == null);

            if (card == null)
            {
                return NotFound();
            }

            // ============================================================
            // POINTS DE VIE
            // ============================================================

            int? healthPoints = card.Type switch
            {
                CardType.Leader => await _context.LeaderCards
                    .AsNoTracking()
                    .Where(x => x.CardId == card.Id)
                    .Select(x => (int?)x.HealthPoints)
                    .FirstOrDefaultAsync(),

                CardType.Character => await _context.CharacterCards
                    .AsNoTracking()
                    .Where(x => x.CardId == card.Id)
                    .Select(x => (int?)x.HealthPoints)
                    .FirstOrDefaultAsync(),

                _ => null
            };

            // ============================================================
            // VIEW MODEL
            // ============================================================

            var model = new CreateCardViewModel
            {
                /*
                 * IMPORTANT :
                 * l'Id envoyé au formulaire est toujours celui
                 * de la carte principale.
                 */
                Id = card.Id,

                Name = card.Name,
                Type = card.Type,
                Rarity = card.Rarity,

                RegionId = card.RegionId,
                CountryId = card.CountryId,

                CardSetId = card.CardSetId,

                CollectionNumber = card.CollectionNumber,
                UsageCost = card.Type == CardType.Leader ? 0 : card.UsageCost,

                HealthPoints = healthPoints,

                MainImageUrl =
                    card.ImageUrl ?? string.Empty,

                // ========================================================
                // VARIANTES = AUTRES CARDS
                // ========================================================

                Variants = card.Variants
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.VariantNumber)
                    .Select(x => new CreateCardVariantViewModel
                    {
                        /*
                         * Ici Id = Card.Id de la variante.
                         */
                        Id = x.Id,

                        VariantNumber =
                            x.VariantNumber,

                        ArtType =
                            x.ArtType,

                        ImageUrl =
                            x.ImageUrl ?? string.Empty
                    })
                    .ToList(),

                // ========================================================
                // CAPACITÉS DE LA CARTE PRINCIPALE
                // ========================================================

                Abilities = card.Abilities
                    .OrderBy(x => x.DisplayOrder)
                    .Select(x => CardAbilityEditor.Read(x))
                    .ToList()
            };

            return View(
                "~/Views/Admin/Cards/Create.cshtml",
                model);
        }

        /// <summary>
        /// Crée ou modifie une carte normale ainsi que ses variantes.
        ///
        /// Une variante est une Card à part entière possédant :
        /// - son propre Id,
        /// - un BaseCardId,
        /// - un VariantNumber,
        /// - un ArtType,
        /// - sa propre ImageUrl.
        ///
        /// Les données gameplay restent portées par la carte principale.
        /// </summary>
        [HttpPost("Cards/Create/{id:int?}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCard(
            int? id,
            CreateCardViewModel Input)
        {
            // ============================================================
            // IDENTIFICATION DU MODE
            // ============================================================

            int? baseCardId = null;

            var requestedId =
                id ?? Input.Id;

            if (id.HasValue &&
                Input.Id.HasValue &&
                id.Value != Input.Id.Value)
            {
                return BadRequest();
            }

            if (requestedId.HasValue)
            {
                var requestedCard = await _context.Cards
                    .AsNoTracking()
                    .Where(x => x.Id == requestedId.Value)
                    .Select(x => new
                    {
                        x.Id,
                        x.BaseCardId
                    })
                    .FirstOrDefaultAsync();

                if (requestedCard == null)
                {
                    return NotFound();
                }

                baseCardId =
                    requestedCard.BaseCardId
                    ?? requestedCard.Id;

                /*
                 * Le formulaire travaille toujours
                 * sur la carte principale.
                 */
                Input.Id = baseCardId;
            }

            var isEdit =
                baseCardId.HasValue;

            // ============================================================
            // NETTOYAGE
            // ============================================================

            if (Input.Type == CardType.Leader)
            {
                Input.UsageCost = 0;
                ModelState.Remove(nameof(Input.UsageCost));
            }

            Input.Name =
                Input.Name?.Trim()
                ?? string.Empty;

            Input.MainImageUrl =
                Input.MainImageUrl?.Trim()
                ?? string.Empty;

            foreach (var variant in Input.Variants)
            {
                variant.ImageUrl =
                    variant.ImageUrl?.Trim()
                    ?? string.Empty;
            }

            foreach (var ability in Input.Abilities)
            {
                ability.Name =
                    ability.Name?.Trim()
                    ?? string.Empty;

                ability.Description =
                    ability.Description?.Trim()
                    ?? string.Empty;
            }

            foreach (var ability in Input.Abilities)
                foreach (var error in CardAbilityEditor.Validate(Input.Type, ability))
                    ModelState.AddModelError(nameof(Input.Abilities), error);

            var abilityRegionIds = Input.Abilities.SelectMany(a => a.Effects.SelectMany(e => e.RegionIds.Concat(e.CountRegionIds)).Concat(a.Conditions.SelectMany(c => c.RegionIds))).Distinct().ToList();
            var abilityCountryIds = Input.Abilities.SelectMany(a => a.Effects.SelectMany(e => e.CountryIds.Concat(e.CountCountryIds)).Concat(a.Conditions.SelectMany(c => c.CountryIds))).Distinct().ToList();
            if (abilityRegionIds.Count > 0 && await _context.Regions.CountAsync(r => abilityRegionIds.Contains(r.Id)) != abilityRegionIds.Count)
                ModelState.AddModelError(nameof(Input.Abilities), "Une région sélectionnée dans les capacités n'existe pas.");
            if (abilityCountryIds.Count > 0 && await _context.Countries.CountAsync(c => abilityCountryIds.Contains(c.Id)) != abilityCountryIds.Count)
                ModelState.AddModelError(nameof(Input.Abilities), "Un pays sélectionné dans les capacités n'existe pas.");

            // ============================================================
            // EXTENSION
            // ============================================================

            var cardSetExists =
                await _context.CardSets
                    .AnyAsync(x =>
                        x.Id == Input.CardSetId);

            if (!cardSetExists)
            {
                ModelState.AddModelError(
                    nameof(Input.CardSetId),
                    "L'extension sélectionnée n'existe pas.");
            }

            // ============================================================
            // RÉGION
            // ============================================================

            var regionExists =
                await _context.Regions
                    .AnyAsync(x =>
                        x.Id == Input.RegionId);

            if (!regionExists)
            {
                ModelState.AddModelError(
                    nameof(Input.RegionId),
                    "La région sélectionnée n'existe pas.");
            }

            // ============================================================
            // PAYS
            // ============================================================

            if (Input.CountryId.HasValue)
            {
                var countryExists =
                    await _context.Countries
                        .AnyAsync(x =>
                            x.Id == Input.CountryId.Value);

                if (!countryExists)
                {
                    ModelState.AddModelError(
                        nameof(Input.CountryId),
                        "Le pays sélectionné n'existe pas.");
                }
            }

            // ============================================================
            // UNICITÉ DU NUMÉRO
            // ============================================================

            /*
             * IMPORTANT :
             * on ne teste l'unicité que sur les cartes normales.
             *
             * Les variantes ont volontairement le même :
             * CardSetId
             * RegionId
             * CollectionNumber
             */
            var duplicateCard =
                await _context.Cards
                    .AnyAsync(x =>
                        x.BaseCardId == null &&
                        (!baseCardId.HasValue ||
                         x.Id != baseCardId.Value) &&
                        x.CardSetId == Input.CardSetId &&
                        x.RegionId == Input.RegionId &&
                        x.CollectionNumber == Input.CollectionNumber);

            if (duplicateCard)
            {
                ModelState.AddModelError(
                    nameof(Input.CollectionNumber),
                    "Une autre carte normale avec ce numéro existe déjà dans cette région et cette extension.");
            }

            // ============================================================
            // RARETÉ
            // ============================================================

            if (Input.Type == CardType.Leader)
            {
                Input.Rarity = null;
            }
            else if (!Input.Rarity.HasValue)
            {
                ModelState.AddModelError(
                    nameof(Input.Rarity),
                    "La rareté est requise pour ce type de carte.");
            }

            // ============================================================
            // POINTS DE VIE
            // ============================================================

            if ((Input.Type == CardType.Leader ||
                 Input.Type == CardType.Character) &&
                !Input.HealthPoints.HasValue)
            {
                ModelState.AddModelError(
                    nameof(Input.HealthPoints),
                    "Les points de vie sont requis pour ce type de carte.");
            }

            // ============================================================
            // VARIANTES
            // ============================================================

            for (var i = 0;
                 i < Input.Variants.Count;
                 i++)
            {
                var variant =
                    Input.Variants[i];

                if (variant.ArtType ==
                    CardArtType.Normal)
                {
                    ModelState.AddModelError(
                        $"Variants[{i}].ArtType",
                        "Une variante ne peut pas utiliser le type Normal.");
                }

                if (string.IsNullOrWhiteSpace(
                    variant.ImageUrl))
                {
                    ModelState.AddModelError(
                        $"Variants[{i}].ImageUrl",
                        "L'image de la variante est obligatoire.");
                }
            }

            // ============================================================
            // VALIDATION
            // ============================================================

            if (!ModelState.IsValid)
            {
                await LoadReferenceData();

                return View(
                    "~/Views/Admin/Cards/Create.cshtml",
                    Input);
            }

            // ============================================================
            // TRANSACTION
            // ============================================================

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                Card card;

                // ========================================================
                // CRÉATION
                // ========================================================

                if (!isEdit)
                {
                    // ----------------------------------------------------
                    // CARTE PRINCIPALE
                    // ----------------------------------------------------

                    card = new Card
                    {
                        Name = Input.Name,
                        Type = Input.Type,
                        Rarity = Input.Rarity,

                        RegionId = Input.RegionId,
                        CountryId = Input.CountryId,

                        CardSetId = Input.CardSetId,

                        CollectionNumber =
                            Input.CollectionNumber,

                        UsageCost =
                            Input.UsageCost,

                        ImageUrl =
                            Input.MainImageUrl,

                        /*
                         * Carte normale.
                         */
                        BaseCardId = null,
                        VariantNumber = null,
                        ArtType = CardArtType.Normal,
                        IsActive = true
                    };

                    _context.Cards.Add(card);

                    /*
                     * Nécessaire pour récupérer card.Id
                     * avant de créer les variantes.
                     */
                    await _context.SaveChangesAsync();

                    // ----------------------------------------------------
                    // VARIANTES
                    // ----------------------------------------------------

                    for (var i = 0;
                         i < Input.Variants.Count;
                         i++)
                    {
                        var inputVariant =
                            Input.Variants[i];

                        var variantCard =
                            new Card
                            {
                                // Données communes
                                Name = card.Name,
                                Type = card.Type,
                                Rarity = card.Rarity,

                                RegionId =
                                    card.RegionId,

                                CountryId =
                                    card.CountryId,

                                CardSetId =
                                    card.CardSetId,

                                CollectionNumber =
                                    card.CollectionNumber,

                                UsageCost =
                                    card.UsageCost,

                                // Données spécifiques variante
                                BaseCardId =
                                    card.Id,

                                VariantNumber =
                                    i + 1,

                                ArtType =
                                    inputVariant.ArtType,

                                ImageUrl =
                                    inputVariant.ImageUrl,

                                IsActive =
                                    true
                            };

                        _context.Cards.Add(
                            variantCard);
                    }

                    // ----------------------------------------------------
                    // CAPACITÉS
                    // ----------------------------------------------------

                    foreach (var ability in
                             Input.Abilities
                                 .OrderBy(x =>
                                     x.DisplayOrder))
                    {
                        /*
                         * Les capacités appartiennent uniquement
                         * à la carte principale.
                         */
                        var createdAbility = new CardAbility
                            {
                                CardId =
                                    card.Id,

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
                            };
                        CardAbilityEditor.Write(createdAbility, ability);
                        _context.CardAbilities.Add(createdAbility);
                    }

                    // ----------------------------------------------------
                    // SPÉCIALISATION
                    // ----------------------------------------------------

                    /*
                     * LeaderCard / CharacterCard / etc.
                     * uniquement sur la carte principale.
                     */
                    await CreateSpecializedCard(
                        card,
                        Input);
                }

                // ========================================================
                // MODIFICATION
                // ========================================================

                else
                {
                    card = await _context.Cards
                        .Include(x => x.Variants)
                        .Include(x => x.Abilities).ThenInclude(a => a.Effects)
                .Include(x => x.Abilities).ThenInclude(a => a.Conditions)
                        .FirstOrDefaultAsync(x =>
                            x.Id == baseCardId!.Value &&
                            x.BaseCardId == null)
                        ?? throw new InvalidOperationException(
                            "La carte principale à modifier n'existe pas.");

                    // ----------------------------------------------------
                    // CARTE PRINCIPALE
                    // ----------------------------------------------------

                    card.Name =
                        Input.Name;

                    card.Type =
                        Input.Type;

                    card.Rarity =
                        Input.Rarity;

                    card.RegionId =
                        Input.RegionId;

                    card.CountryId =
                        Input.CountryId;

                    card.CardSetId =
                        Input.CardSetId;

                    card.CollectionNumber =
                        Input.CollectionNumber;

                    card.UsageCost =
                        Input.UsageCost;

                    card.ImageUrl =
                        Input.MainImageUrl;

                    card.BaseCardId =
                        null;

                    card.VariantNumber =
                        null;

                    card.ArtType =
                        CardArtType.Normal;

                    card.IsActive =
                        true;

                    // ----------------------------------------------------
                    // VARIANTES EXISTANTES
                    // ----------------------------------------------------

                    /*
                     * On charge également les variantes désactivées
                     * afin de ne jamais réutiliser leur VariantNumber.
                     */
                    var existingVariants =
                        card.Variants
                            .ToList();

                    // ----------------------------------------------------
                    // SYNCHRONISATION DES DONNÉES COMMUNES
                    // ----------------------------------------------------

                    foreach (var variant in
                             existingVariants)
                    {
                        SyncVariantSharedData(
                            variant,
                            card);
                    }

                    // ----------------------------------------------------
                    // IDS ENVOYÉS PAR LE FORMULAIRE
                    // ----------------------------------------------------

                    var submittedVariantIds =
                        Input.Variants
                            .Where(x =>
                                x.Id.HasValue)
                            .Select(x =>
                                x.Id!.Value)
                            .ToHashSet();

                    // ----------------------------------------------------
                    // SÉCURITÉ DES IDS
                    // ----------------------------------------------------

                    foreach (var inputVariant in
                             Input.Variants
                                 .Where(x =>
                                     x.Id.HasValue))
                    {
                        var belongsToCard =
                            existingVariants.Any(x =>
                                x.Id ==
                                inputVariant.Id!.Value);

                        if (!belongsToCard)
                        {
                            ModelState.AddModelError(
                                string.Empty,
                                "Une variante sélectionnée n'appartient pas à cette carte.");

                            await transaction
                                .RollbackAsync();

                            await LoadReferenceData();

                            return View(
                                "~/Views/Admin/Cards/Create.cshtml",
                                Input);
                        }
                    }

                    // ----------------------------------------------------
                    // VARIANTES RETIRÉES
                    // ----------------------------------------------------

                    /*
                     * On ne les supprime pas physiquement.
                     *
                     * Cela permet de conserver les références :
                     * inventaire,
                     * historique de booster,
                     * transactions,
                     * etc.
                     */
                    foreach (var existingVariant in
                             existingVariants)
                    {
                        if (!submittedVariantIds.Contains(
                            existingVariant.Id))
                        {
                            existingVariant.IsActive =
                                false;
                        }
                    }

                    // ----------------------------------------------------
                    // PROCHAIN NUMÉRO
                    // ----------------------------------------------------

                    /*
                     * On prend aussi les variantes désactivées.
                     *
                     * Exemple :
                     *
                     * -1 supprimée
                     * -2 existante
                     *
                     * la prochaine sera -3,
                     * jamais -1.
                     */
                    var nextVariantNumber =
                        existingVariants.Count > 0
                            ? existingVariants
                                .Where(x =>
                                    x.VariantNumber.HasValue)
                                .Select(x =>
                                    x.VariantNumber!.Value)
                                .DefaultIfEmpty(0)
                                .Max() + 1
                            : 1;

                    // ----------------------------------------------------
                    // UPDATE / INSERT DES VARIANTES
                    // ----------------------------------------------------

                    foreach (var inputVariant in
                             Input.Variants)
                    {
                        // =================================================
                        // VARIANTE EXISTANTE
                        // =================================================

                        if (inputVariant.Id.HasValue)
                        {
                            var existingVariant =
                                existingVariants
                                    .First(x =>
                                        x.Id ==
                                        inputVariant.Id.Value);

                            SyncVariantSharedData(
                                existingVariant,
                                card);

                            existingVariant.ArtType =
                                inputVariant.ArtType;

                            existingVariant.ImageUrl =
                                inputVariant.ImageUrl;

                            existingVariant.IsActive =
                                true;

                            /*
                             * IMPORTANT :
                             * on ne modifie jamais
                             * VariantNumber.
                             */

                            continue;
                        }

                        // =================================================
                        // NOUVELLE VARIANTE
                        // =================================================

                        var newVariant =
                            new Card
                            {
                                BaseCardId =
                                    card.Id,

                                VariantNumber =
                                    nextVariantNumber++,

                                ArtType =
                                    inputVariant.ArtType,

                                ImageUrl =
                                    inputVariant.ImageUrl,

                                IsActive =
                                    true
                            };

                        SyncVariantSharedData(
                            newVariant,
                            card);

                        _context.Cards.Add(
                            newVariant);
                    }

                    // ----------------------------------------------------
                    // CAPACITÉS
                    // ----------------------------------------------------

                    var existingAbilities =
                        card.Abilities
                            .ToList();

                    var submittedAbilityIds =
                        Input.Abilities
                            .Where(x =>
                                x.Id.HasValue)
                            .Select(x =>
                                x.Id!.Value)
                            .ToHashSet();

                    // Supprime les capacités retirées.
                    foreach (var existingAbility in
                             existingAbilities)
                    {
                        if (!submittedAbilityIds.Contains(
                            existingAbility.Id))
                        {
                            _context.CardAbilities.Remove(
                                existingAbility);
                        }
                    }

                    // Update / Insert
                    foreach (var inputAbility in
                             Input.Abilities)
                    {
                        CardAbility ability;

                        if (inputAbility.Id.HasValue)
                        {
                            ability =
                                existingAbilities
                                    .FirstOrDefault(x =>
                                        x.Id ==
                                        inputAbility.Id.Value)
                                ?? throw new InvalidOperationException(
                                    "Une capacité sélectionnée n'appartient pas à cette carte.");
                        }
                        else
                        {
                            ability =
                                new CardAbility
                                {
                                    CardId =
                                        card.Id
                                };

                            _context.CardAbilities.Add(
                                ability);
                        }

                        ability.Type =
                            inputAbility.Type;

                        ability.Name =
                            inputAbility.Name;

                        ability.Description =
                            inputAbility.Description;

                        ability.UsageCost =
                            inputAbility.UsageCost;

                        ability.DisplayOrder =
                            inputAbility.DisplayOrder;
                        CardAbilityEditor.Write(ability, inputAbility);
                    }

                    // ----------------------------------------------------
                    // SPÉCIALISATION
                    // ----------------------------------------------------

                    await UpdateSpecializedCard(
                        card,
                        Input);
                }

                // ========================================================
                // SAVE
                // ========================================================

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // ========================================================
                // SUCCESS
                // ========================================================

                _logger.LogInformation(
                    "{Action} de la carte {CardName} (ID: {CardId}) réussie.",
                    isEdit
                        ? "Modification"
                        : "Création",
                    card.Name,
                    card.Id);

                TempData["SuccessMessage"] =
                    isEdit
                        ? $"Carte « {card.Name} » modifiée avec succès !"
                        : $"Carte « {card.Name} » créée avec succès !";

                return RedirectToAction(
                    nameof(Cards),
                    new
                    {
                        cardSetId =
                            Input.CardSetId
                    });
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Erreur SQL pendant {Action} de la carte.",
                    isEdit
                        ? "la modification"
                        : "la création");

                ModelState.AddModelError(
                    string.Empty,
                    "Une erreur est survenue lors de l'enregistrement de la carte.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Erreur pendant {Action} de la carte.",
                    isEdit
                        ? "la modification"
                        : "la création");

                ModelState.AddModelError(
                    string.Empty,
                    "Une erreur inattendue est survenue.");
            }

            await LoadReferenceData();

            return View(
                "~/Views/Admin/Cards/Create.cshtml",
                Input);
        }

        /// <summary>
        /// Supprime définitivement une carte.
        ///
        /// Si l'id correspond à une variante,
        /// seule cette variante est supprimée.
        ///
        /// Si l'id correspond à une carte principale,
        /// la carte principale et toutes ses variantes sont supprimées.
        ///
        /// La suppression est refusée lorsqu'une des cartes concernées
        /// appartient déjà à un utilisateur.
        /// </summary>
        [HttpPost("Cards/Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCard(int id)
        {
            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                // ========================================================
                // CARTE
                // ========================================================

                var card = await _context.Cards
                    .Include(x => x.Variants)
                    .Include(x => x.Abilities).ThenInclude(a => a.Effects)
                .Include(x => x.Abilities).ThenInclude(a => a.Conditions)
                    .FirstOrDefaultAsync(x =>
                        x.Id == id);

                if (card == null)
                {
                    return NotFound(
                        $"La carte avec l'identifiant {id} n'existe pas.");
                }

                var cardSetId =
                    card.CardSetId;

                var cardName =
                    card.Name;

                var isVariant =
                    card.BaseCardId.HasValue;

                // ========================================================
                // IDS QUI SERONT SUPPRIMÉS
                // ========================================================

                List<int> cardIdsToDelete;

                if (isVariant)
                {
                    /*
                     * Variante :
                     * seulement cette Card.
                     */
                    cardIdsToDelete =
                        new List<int>
                        {
                    card.Id
                        };
                }
                else
                {
                    /*
                     * Carte normale :
                     * elle-même + toutes ses variantes.
                     */
                    cardIdsToDelete =
                        card.Variants
                            .Select(x => x.Id)
                            .Prepend(card.Id)
                            .ToList();
                }

                // ========================================================
                // INVENTAIRES
                // ========================================================


                var inventoryEntries = await _context.UserCardCollections
                    .Where(x => cardIdsToDelete.Contains(x.CardId))
                    .ToListAsync();

                var removedInventoryCount = inventoryEntries.Count;

                if (inventoryEntries.Count > 0)
                {
                    _context.UserCardCollections.RemoveRange(inventoryEntries);
                }


                // --------------------------------------------------------
                // HISTORIQUES D'OUVERTURE DE BOOSTERS
                // --------------------------------------------------------

                var boosterOpeningCards = await _context.BoosterOpeningCards
                    .Where(x => cardIdsToDelete.Contains(x.CardId))
                    .ToListAsync();

                var removedBoosterHistoryCount = boosterOpeningCards.Count;

                if (boosterOpeningCards.Count > 0)
                {
                    _context.BoosterOpeningCards.RemoveRange(boosterOpeningCards);
                }

                _logger.LogInformation(
                    "Suppression de la carte {CardId} : {InventoryCount} entrée(s) d'inventaire et {BoosterCount} historique(s) de booster supprimé(s).",
                    card.Id,
                    removedInventoryCount,
                    removedBoosterHistoryCount);
                // ========================================================
                // VARIANTE SEULE
                // ========================================================

                if (isVariant)
                {
                    /*
                     * Une variante ne possède normalement :
                     * - aucune capacité propre,
                     * - aucune spécialisation propre.
                     *
                     * C'est donc simplement une Card.
                     */
                    _context.Cards.Remove(card);

                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Variante supprimée : {CardName} (ID: {CardId})",
                        cardName,
                        card.Id);

                    TempData["SuccessMessage"] =
                        $"La variante de « {cardName} » a été supprimée avec succès.";

                    return RedirectToAction(
                        nameof(Cards),
                        new
                        {
                            cardSetId
                        });
                }

                // ========================================================
                // CARTE PRINCIPALE
                // ========================================================

                // --------------------------------------------------------
                // DONNÉES SPÉCIALISÉES
                // --------------------------------------------------------

                var leaderCard =
                    await _context.LeaderCards
                        .FirstOrDefaultAsync(x =>
                            x.CardId == card.Id);

                if (leaderCard != null)
                {
                    _context.LeaderCards.Remove(
                        leaderCard);
                }

                var characterCard =
                    await _context.CharacterCards
                        .FirstOrDefaultAsync(x =>
                            x.CardId == card.Id);

                if (characterCard != null)
                {
                    _context.CharacterCards.Remove(
                        characterCard);
                }

                var politicalDecisionCard =
                    await _context.PoliticalDecisionCards
                        .FirstOrDefaultAsync(x =>
                            x.CardId == card.Id);

                if (politicalDecisionCard != null)
                {
                    _context.PoliticalDecisionCards.Remove(
                        politicalDecisionCard);
                }

                var locationCard =
                    await _context.LocationCards
                        .FirstOrDefaultAsync(x =>
                            x.CardId == card.Id);

                if (locationCard != null)
                {
                    _context.LocationCards.Remove(
                        locationCard);
                }

                // --------------------------------------------------------
                // CAPACITÉS
                // --------------------------------------------------------

                if (card.Abilities.Count > 0)
                {
                    _context.CardAbilities.RemoveRange(
                        card.Abilities);
                }

                // --------------------------------------------------------
                // VARIANTES
                // --------------------------------------------------------

                /*
                 * Les variantes sont désormais elles-mêmes des Cards.
                 */
                if (card.Variants.Count > 0)
                {
                    _context.Cards.RemoveRange(
                        card.Variants);
                }

                // --------------------------------------------------------
                // CARTE PRINCIPALE
                // --------------------------------------------------------

                _context.Cards.Remove(
                    card);

                // ========================================================
                // SAVE
                // ========================================================

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Carte supprimée : {CardName} (ID: {CardId}) avec {VariantCount} variante(s).",
                    cardName,
                    card.Id,
                    card.Variants.Count);

                TempData["SuccessMessage"] =
                    $"Carte « {cardName} » et ses variantes supprimées avec succès.";

                return RedirectToAction(
                    nameof(Cards),
                    new
                    {
                        cardSetId
                    });
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Erreur SQL lors de la suppression de la carte {CardId}.",
                    id);

                TempData["ErrorMessage"] =
                    "La carte ne peut pas être supprimée car elle est encore référencée par d'autres données.";

                return RedirectToAction(
                    nameof(Cards));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Erreur inattendue lors de la suppression de la carte {CardId}.",
                    id);

                TempData["ErrorMessage"] =
                    "Une erreur inattendue est survenue pendant la suppression.";

                return RedirectToAction(
                    nameof(Cards));
            }
        }

        /// <summary>
        /// Met à jour la table spécialisée correspondant au type de carte.
        /// </summary>
        private async Task UpdateSpecializedCard(
            Card card,
            EditCardViewModel input)
        {
            var leader = await _context.LeaderCards
                .FirstOrDefaultAsync(x => x.CardId == card.Id);

            var character = await _context.CharacterCards
                .FirstOrDefaultAsync(x => x.CardId == card.Id);

            var politicalDecision = await _context.PoliticalDecisionCards
                .FirstOrDefaultAsync(x => x.CardId == card.Id);

            var location = await _context.LocationCards
                .FirstOrDefaultAsync(x => x.CardId == card.Id);

            // --------------------------------------------------------
            // Supprime les anciennes spécialisations qui ne
            // correspondent plus au nouveau type.
            // --------------------------------------------------------

            if (input.Type != CardType.Leader &&
                leader != null)
            {
                _context.LeaderCards.Remove(leader);
                leader = null;
            }

            if (input.Type != CardType.Character &&
                character != null)
            {
                _context.CharacterCards.Remove(character);
                character = null;
            }

            if (input.Type != CardType.PoliticalDecision &&
                politicalDecision != null)
            {
                _context.PoliticalDecisionCards.Remove(politicalDecision);
                politicalDecision = null;
            }

            if (input.Type != CardType.Location &&
                location != null)
            {
                _context.LocationCards.Remove(location);
                location = null;
            }

            // --------------------------------------------------------
            // Recrée / met à jour la spécialisation.
            // --------------------------------------------------------

            switch (input.Type)
            {
                case CardType.Leader:

                    if (leader == null)
                    {
                        leader = new LeaderCard
                        {
                            CardId = card.Id
                        };

                        _context.LeaderCards.Add(leader);
                    }

                    leader.HealthPoints =
                        input.HealthPoints!.Value;

                    break;

                case CardType.Character:

                    if (character == null)
                    {
                        character = new CharacterCard
                        {
                            CardId = card.Id
                        };

                        _context.CharacterCards.Add(character);
                    }

                    character.HealthPoints =
                        input.HealthPoints!.Value;

                    break;

                case CardType.PoliticalDecision:

                    if (politicalDecision == null)
                    {
                        politicalDecision = new PoliticalDecisionCard
                        {
                            CardId = card.Id
                        };

                        _context.PoliticalDecisionCards.Add(
                            politicalDecision);
                    }

                    break;

                case CardType.Location:

                    if (location == null)
                    {
                        location = new LocationCard
                        {
                            CardId = card.Id
                        };

                        _context.LocationCards.Add(location);
                    }

                    break;
            }
        }


        // ============================================================
        // DONNÉES DE RÉFÉRENCE
        // ============================================================

        /// <summary>
        /// Charge toutes les données nécessaires aux listes
        /// déroulantes du formulaire de création.
        /// </summary>
        private async Task LoadReferenceData()
        {
            var cardSets = await _context.CardSets
                .AsNoTracking()
                .OrderBy(cs => cs.Name)
                .ToListAsync();

            ViewBag.CardSets = new SelectList(
                cardSets,
                "Id",
                "Name");

            var regions = await _context.Regions
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .ToListAsync();

            ViewBag.Regions = new SelectList(
                regions,
                "Id",
                "Name");

            var countries = await _context.Countries
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Countries = new SelectList(
                countries,
                "Id",
                "Name");

            ViewBag.CardTypes =
                new SelectList(GetCardTypeOptions(), "Value", "Text");

            ViewBag.CardRarities =
                new SelectList(GetCardRarityOptions(), "Value", "Text");

            ViewBag.CardArtTypes =
                new SelectList(GetCardArtTypeOptions(), "Value", "Text");

            ViewBag.AbilityTypes =
                new SelectList(GetAbilityTypeOptions(), "Value", "Text");
        }

        private static List<SelectListItem> GetCardTypeOptions()
        {
            return new List<SelectListItem>
            {
                new()
                {
                    Value = ((int)CardType.Leader).ToString(),
                    Text = "Président / Leader"
                },
                new()
                {
                    Value = ((int)CardType.Character).ToString(),
                    Text = "Personnage / Politicien"
                },
                new()
                {
                    Value = ((int)CardType.PoliticalDecision).ToString(),
                    Text = "Décision Politique"
                },
                new()
                {
                    Value = ((int)CardType.Location).ToString(),
                    Text = "Lieu"
                }
            };
        }

        private static List<SelectListItem> GetCardRarityOptions()
        {
            return new List<SelectListItem>
            {
                new()
                {
                    Value = "",
                    Text = "--- Sélectionner une rareté ---"
                },
                new()
                {
                    Value = ((int)CardRarity.Common).ToString(),
                    Text = "Commune (C)"
                },
                new()
                {
                    Value = ((int)CardRarity.Rare).ToString(),
                    Text = "Rare (R)"
                },
                new()
                {
                    Value = ((int)CardRarity.SuperRare).ToString(),
                    Text = "Super Rare (SR)"
                },
                new()
                {
                    Value = ((int)CardRarity.UltraRare).ToString(),
                    Text = "Ultra Rare (UR)"
                }
            };
        }

        private static List<SelectListItem> GetCardArtTypeOptions()
        {
            return new List<SelectListItem>
            {
                new()
                {
                    Value = ((int)CardArtType.Normal).ToString(),
                    Text = "Normal"
                },
                new()
                {
                    Value = ((int)CardArtType.FullArt).ToString(),
                    Text = "Full Art"
                },
                new()
                {
                    Value = ((int)CardArtType.AlternateArt).ToString(),
                    Text = "Alternate Art"
                }
            };
        }

        private static List<SelectListItem> GetAbilityTypeOptions()
        {
            return new List<SelectListItem>
            {
                new()
                {
                    Value = ((int)AbilityType.Skill).ToString(),
                    Text = "Compétence ⚔"
                },
                new()
                {
                    Value = ((int)AbilityType.Trait).ToString(),
                    Text = "Talent ✦"
                },
                new()
                {
                    Value = ((int)AbilityType.Effect).ToString(),
                    Text = "Effet ◆"
                }
            };
        }

        /// <summary>
        /// Retourne un aperçu HTML de la carte basé sur les données du formulaire.
        /// </summary>
        [HttpPost("PreviewCard")]
        public IActionResult PreviewCard(
            [FromBody] CardPreviewViewModel previewData)
        {
            try
            {
                var cardViewModel = new CardViewModel
                {
                    Name = previewData.Name,
                    Type = (CardType)previewData.Type,
                    Rarity = previewData.Rarity.HasValue
                        ? (CardRarity)previewData.Rarity.Value
                        : null,

                    UsageCost = previewData.UsageCost,

                    HealthPoints = previewData.HealthPoints,

                    ImageUrl = string.IsNullOrWhiteSpace(
                        previewData.ImageUrl)
                            ? "https://via.placeholder.com/250x350?text=Carte"
                            : previewData.ImageUrl,

                    ArtType = (CardArtType)previewData.MainArtType,

                    CollectionNumber =
                        string.IsNullOrWhiteSpace(
                            previewData.CollectionNumber)
                            ? "XX/XX"
                            : previewData.CollectionNumber,

                    RegionName =
                        string.IsNullOrWhiteSpace(
                            previewData.RegionName)
                            ? "Région"
                            : previewData.RegionName,

                    CountryName =
                        previewData.CountryName ?? string.Empty,

                    RegionColor =
                        string.IsNullOrWhiteSpace(
                            previewData.RegionColor)
                            ? "#FFD700"
                            : previewData.RegionColor,

                    CardSetCode = previewData.CardSetCode,

                    Abilities = previewData.Abilities
                        .OrderBy(x => x.DisplayOrder)
                        .Select(x => new CardAbilityViewModel
                        {
                            Type = (AbilityType)x.Type,
                            UsageCost = x.UsageCost,
                            Name = x.Name,
                            Description = x.Description,
                            DisplayOrder = x.DisplayOrder
                        })
                        .ToList()
                };

                return ViewComponent(
                    "Card",
                    cardViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de la prévisualisation de la carte.");

                return BadRequest(
                    "Erreur lors de la prévisualisation de la carte.");
            }
        }

        /// <summary>
        /// Affiche la liste des extensions disponibles dans le TCG.
        /// </summary>
        /// <returns></returns>
        [HttpGet("CardSets")]
        public async Task<IActionResult> CardSets()
        {
            var cardSets = await _context.CardSets
                .AsNoTracking()
                .OrderByDescending(x => x.ReleaseDate)
                .ToListAsync();

            return View("~/Views/Admin/CardSets/Index.cshtml", cardSets);
        }

        [HttpGet("CardSets/Create")]
        public IActionResult CreateCardSet()
        {
            var model = new CreateCardSetViewModel
            {
                ReleaseDate = DateTime.Today,
                IsActive = false,
                BoosterPrice = 0
            };

            return View(
                "~/Views/Admin/CardSets/Create.cshtml",
                model);
        }

        [HttpPost("CardSets/Create")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> CreateCardSet(CreateCardSetViewModel Input)
            => SaveCardSetAsync(Input, null);

        [HttpGet("CardSets/Edit/{id:int}")]
        public async Task<IActionResult> EditCardSet(int id)
        {
            var cardSet = await _context.CardSets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (cardSet == null) return NotFound();

            ViewData["CardSetId"] = id;
            ViewData["ExistingBoosterImageUrl"] = cardSet.BoosterImageUrl;
            return View("~/Views/Admin/CardSets/Create.cshtml", new CreateCardSetViewModel
            {
                Name = cardSet.Name,
                Code = cardSet.Code,
                IsActive = cardSet.IsActive,
                ReleaseDate = cardSet.ReleaseDate,
                BoosterImageUrl = cardSet.BoosterImageUrl,
                BoosterPrice = cardSet.BoosterPrice
            });
        }

        [HttpPost("CardSets/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCardSet(int id, CreateCardSetViewModel Input)
        {
            var cardSet = await _context.CardSets.FirstOrDefaultAsync(x => x.Id == id);
            if (cardSet == null) return NotFound();
            return await SaveCardSetAsync(Input, cardSet);
        }

        private async Task<IActionResult> SaveCardSetAsync(CreateCardSetViewModel Input, CardSet? existing)
        {
            var isEditing = existing != null;
            var excludedId = existing?.Id ?? 0;
            ViewData["CardSetId"] = existing?.Id;
            ViewData["ExistingBoosterImageUrl"] = existing?.BoosterImageUrl;
            // Nettoyage des valeurs texte
            Input.Name = Input.Name?.Trim() ?? string.Empty;
            Input.Code = Input.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            Input.BoosterImageUrl = Input.BoosterImageUrl?.Trim() ?? string.Empty;
            // Une ancienne illustration peut être conservée sans imposer un nouvel import.
            var keepExistingImage = isEditing && !string.IsNullOrWhiteSpace(existing!.BoosterImageUrl)
                && Input.BoosterImageUrl == existing.BoosterImageUrl;
            if (keepExistingImage)
                ModelState.Remove(nameof(Input.BoosterImageUrl));
            else if (System.Text.RegularExpressions.Regex.IsMatch(Input.BoosterImageUrl, @"^/uploads/cardsets/[a-f0-9]{32}\.(png|jpg|webp)$"))
            {
                var environment = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var imagePath = Path.Combine(environment.WebRootPath, "uploads", "cardsets", Path.GetFileName(Input.BoosterImageUrl));
                if (!System.IO.File.Exists(imagePath))
                    ModelState.AddModelError(nameof(Input.BoosterImageUrl), "L’image du booster est introuvable. Importez-la à nouveau.");
            }

            // Vérification du code
            var codeExists = await _context.CardSets
                .AnyAsync(x => x.Code == Input.Code && x.Id != excludedId);

            if (codeExists)
            {
                ModelState.AddModelError( nameof(Input.Code), "Ce code d'extension existe déjà.");
            }

            // Vérification du nom
            var nameExists = await _context.CardSets
                .AnyAsync(x => x.Name.ToLower() == Input.Name.ToLower() && x.Id != excludedId);

            if (nameExists)
            {
                ModelState.AddModelError(
                    nameof(Input.Name),
                    "Une extension portant ce nom existe déjà.");
            }

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/CardSets/Create.cshtml",
                    Input);
            }

            try
            {
                var cardSet = existing ?? new CardSet();
                cardSet.Name = Input.Name;
                cardSet.Code = Input.Code;
                cardSet.IsActive = Input.IsActive;
                cardSet.ReleaseDate = DateTime.SpecifyKind(Input.ReleaseDate, DateTimeKind.Utc);
                cardSet.BoosterImageUrl = Input.BoosterImageUrl;
                cardSet.BoosterPrice = Input.BoosterPrice;

                if (!isEditing) _context.CardSets.Add(cardSet);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = isEditing
                    ? $"L'extension « {cardSet.Name} » a été modifiée avec succès."
                    : $"L'extension « {cardSet.Name} » a été créée avec succès.";

                return RedirectToAction(nameof(CardSets));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Erreur lors de l'enregistrement de l'extension {CardSetName}.", Input.Name);

                ModelState.AddModelError(string.Empty, "Une erreur est survenue lors de l'enregistrement de l'extension. Rechargez la page et réessayez.");

                return View( "~/Views/Admin/CardSets/Create.cshtml", Input);
            }
        }

        /// <summary>
        /// Affiche la liste des régions.
        /// </summary>
        [HttpGet("Regions")]
        public async Task<IActionResult> Regions()
        {
            var regions = await _context.Regions
                .AsNoTracking()
                .Include(r => r.Countries)
                .Include(r => r.Cards)
                .OrderBy(r => r.Name)
                .ToListAsync();

            return View(
                "~/Views/Admin/Regions/Index.cshtml",
                regions);
        }


        /// <summary>
        /// Affiche le formulaire de modification d'une région.
        /// </summary>
        [HttpGet("Regions/Edit/{id:int}")]
        public async Task<IActionResult> EditRegion(int id)
        {
            var region = await _context.Regions
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (region == null)
            {
                return NotFound();
            }

            var input = new CreateRegionViewModel
            {
                Name = region.Name,
                Code = region.Code,
                Color = region.Color
            };

            ViewBag.RegionId = region.Id;

            return View(
                "~/Views/Admin/Regions/Edit.cshtml",
                input);
        }


        /// <summary>
        /// Modifie une région existante.
        /// </summary>
        [HttpPost("Regions/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRegion(
            int id,
            CreateRegionViewModel Input)
        {
            Input.Name = Input.Name.Trim();
            Input.Code = Input.Code.Trim().ToUpperInvariant();
            Input.Color = Input.Color.Trim().ToUpperInvariant();

            var region = await _context.Regions
                .FirstOrDefaultAsync(r => r.Id == id);

            if (region == null)
            {
                return NotFound();
            }

            var codeExists = await _context.Regions
                .AnyAsync(r =>
                    r.Code == Input.Code &&
                    r.Id != id);

            if (codeExists)
            {
                ModelState.AddModelError(
                    nameof(Input.Code),
                    "Ce code de région existe déjà.");
            }

            var nameExists = await _context.Regions
                .AnyAsync(r =>
                    r.Name.ToLower() == Input.Name.ToLower() &&
                    r.Id != id);

            if (nameExists)
            {
                ModelState.AddModelError(
                    nameof(Input.Name),
                    "Une région portant ce nom existe déjà.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.RegionId = id;

                return View(
                    "~/Views/Admin/Regions/Edit.cshtml",
                    Input);
            }

            try
            {
                region.Name = Input.Name;
                region.Code = Input.Code;
                region.Color = Input.Color;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"La région « {region.Name} » a été modifiée avec succès.";

                return RedirectToAction(nameof(Regions));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de la modification de la région {RegionId}.",
                    id);

                ModelState.AddModelError(
                    string.Empty,
                    "Une erreur est survenue lors de la modification de la région.");

                ViewBag.RegionId = id;

                return View(
                    "~/Views/Admin/Regions/Edit.cshtml",
                    Input);
            }
        }


        /// <summary>
        /// Supprime une région si elle n'est utilisée par aucun pays ni aucune carte.
        /// </summary>
        [HttpPost("Regions/Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRegion(int id)
        {
            var region = await _context.Regions
                .FirstOrDefaultAsync(r => r.Id == id);

            if (region == null)
            {
                return NotFound();
            }

            var hasCountries = await _context.Countries
                .AnyAsync(c => c.RegionId == id);

            if (hasCountries)
            {
                TempData["ErrorMessage"] =
                    $"Impossible de supprimer la région « {region.Name} » " +
                    "car des pays lui sont encore associés.";

                return RedirectToAction(nameof(Regions));
            }

            var hasCards = await _context.Cards
                .AnyAsync(c => c.RegionId == id);

            if (hasCards)
            {
                TempData["ErrorMessage"] =
                    $"Impossible de supprimer la région « {region.Name} » " +
                    "car des cartes lui sont encore associées.";

                return RedirectToAction(nameof(Regions));
            }

            var regionName = region.Name;

            try
            {
                _context.Regions.Remove(region);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"La région « {regionName} » a été supprimée avec succès.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de la suppression de la région {RegionId}.",
                    id);

                TempData["ErrorMessage"] =
                    $"Impossible de supprimer la région « {regionName} ».";
            }

            return RedirectToAction(nameof(Regions));
        }

        /// <summary>
        /// Affiche le formulaire de création d'une nouvelle région.
        /// </summary>
        /// <returns></returns>
        [HttpGet("Regions/Create")]
        public IActionResult CreateRegion()
        {
            return View(
                "~/Views/Admin/Regions/Create.cshtml",
                new CreateRegionViewModel
                {
                    Color = "#FFFFFF"
                });
        }

        [HttpPost("Regions/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRegion(CreateRegionViewModel Input)
        {
            Input.Name = Input.Name.Trim();
            Input.Code = Input.Code.Trim().ToUpperInvariant();
            Input.Color = Input.Color.Trim().ToUpperInvariant();

            var codeExists = await _context.Regions.AnyAsync(r => r.Code == Input.Code);

            if (codeExists)
            {
                ModelState.AddModelError(
                    nameof(Input.Code),
                    "Ce code de région existe déjà.");
            }

            var nameExists = await _context.Regions.AnyAsync(r => r.Name.ToLower() == Input.Name.ToLower());

            if (nameExists)
            {
                ModelState.AddModelError(nameof(Input.Name), "Une région portant ce nom existe déjà.");
            }

            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/Regions/Create.cshtml", Input);
            }

            try
            {
                var region = new Region
                {
                    Name = Input.Name,
                    Code = Input.Code,
                    Color = Input.Color
                };

                _context.Regions.Add(region);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"La région « {region.Name} » a été créée avec succès.";

                return RedirectToAction(nameof(Regions));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de la création de la région {RegionName}.",
                    Input.Name);

                ModelState.AddModelError(
                    string.Empty,
                    "Une erreur est survenue lors de la création de la région.");

                return View(
                    "~/Views/Admin/Regions/Create.cshtml",
                    Input);
            }
        }

        [HttpGet("Countries")]
        public async Task<IActionResult> Countries(int? regionId)
        {
            var query = _context.Countries
                .AsNoTracking()
                .Include(c => c.Region)
                .AsQueryable();

            if (regionId.HasValue)
            {
                query = query.Where(c => c.RegionId == regionId.Value);
            }

            var countries = await query
                .OrderBy(c => c.Region.Name)
                .ThenBy(c => c.Name)
                .ToListAsync();

            ViewBag.RegionId = regionId;

            return View("~/Views/Admin/Countries/Index.cshtml", countries);
        }

        /// <summary>
        /// Affiche le formulaire de création d'un pays.
        /// </summary>
        [HttpGet("Countries/Create")]
        public async Task<IActionResult> CreateCountry()
        {
            await LoadRegionsForCountry();

            return View(
                "~/Views/Admin/Countries/Create.cshtml",
                new CreateCountryViewModel());
        }

        [HttpPost("Countries/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCountry(CreateCountryViewModel Input)
        {
            Input.Name = Input.Name.Trim();
            Input.Code = Input.Code.Trim().ToUpperInvariant();

            if (!Input.RegionId.HasValue)
            {
                ModelState.AddModelError(
                    nameof(Input.RegionId),
                    "Veuillez sélectionner une région.");
            }

            Region? region = null;

            if (Input.RegionId.HasValue)
            {
                region = await _context.Regions
                    .FirstOrDefaultAsync(r => r.Id == Input.RegionId.Value);

                if (region == null)
                {
                    ModelState.AddModelError(
                        nameof(Input.RegionId),
                        "La région sélectionnée n'existe pas.");
                }
            }

            var codeExists = await _context.Countries
                .AnyAsync(c => c.Code == Input.Code);

            if (codeExists)
            {
                ModelState.AddModelError(
                    nameof(Input.Code),
                    "Ce code de pays existe déjà.");
            }

            if (!ModelState.IsValid)
            {
                await LoadRegionsForCountry(Input.RegionId);

                return View("~/Views/Admin/Countries/Create.cshtml", Input);
            }

            try
            {
                var country = new Country
                {
                    Name = Input.Name,
                    Code = Input.Code,
                    RegionId = Input.RegionId!.Value
                };

                _context.Countries.Add(country);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"Le pays « {country.Name} » a été créé avec succès.";

                return RedirectToAction(nameof(Countries));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de la création du pays {CountryName}.",
                    Input.Name);

                ModelState.AddModelError(
                    string.Empty,
                    "Une erreur est survenue lors de la création du pays.");

                await LoadRegionsForCountry(Input.RegionId);

                return View(
                    "~/Views/Admin/Countries/Create.cshtml",
                    Input);
            }
        }

        [HttpGet("Countries/Edit/{id:int}")]
        public async Task<IActionResult> EditCountry(int id)
        {
            var country = await _context.Countries
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (country == null)
            {
                return NotFound();
            }

            var input = new CreateCountryViewModel
            {
                Name = country.Name,
                Code = country.Code,
                RegionId = country.RegionId
            };

            await LoadRegionsForCountry(input.RegionId);

            ViewBag.CountryId = country.Id;

            return View(
                "~/Views/Admin/Countries/Edit.cshtml",
                input);
        }


        [HttpPost("Countries/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCountry(
            int id,
            CreateCountryViewModel Input)
        {
            Input.Name = Input.Name.Trim();
            Input.Code = Input.Code.Trim().ToUpperInvariant();

            if (!Input.RegionId.HasValue)
            {
                ModelState.AddModelError(
                    nameof(Input.RegionId),
                    "Veuillez sélectionner une région.");
            }

            Region? region = null;

            if (Input.RegionId.HasValue)
            {
                region = await _context.Regions
                    .FirstOrDefaultAsync(
                        r => r.Id == Input.RegionId.Value);

                if (region == null)
                {
                    ModelState.AddModelError(
                        nameof(Input.RegionId),
                        "La région sélectionnée n'existe pas.");
                }
            }

            var country = await _context.Countries
                .FirstOrDefaultAsync(c => c.Id == id);

            if (country == null)
            {
                return NotFound();
            }

            /*
             * Le code doit être unique, mais le pays actuel
             * doit évidemment pouvoir conserver son propre code.
             */
            var codeExists = await _context.Countries
                .AnyAsync(c =>
                    c.Code == Input.Code &&
                    c.Id != id);

            if (codeExists)
            {
                ModelState.AddModelError(
                    nameof(Input.Code),
                    "Ce code de pays existe déjà.");
            }

            if (!ModelState.IsValid)
            {
                await LoadRegionsForCountry(Input.RegionId);

                ViewBag.CountryId = id;

                return View(
                    "~/Views/Admin/Countries/Edit.cshtml",
                    Input);
            }

            try
            {
                country.Name = Input.Name;
                country.Code = Input.Code;
                country.RegionId = Input.RegionId!.Value;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"Le pays « {country.Name} » a été modifié avec succès.";

                return RedirectToAction(nameof(Countries));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de la modification du pays {CountryId}.",
                    id);

                ModelState.AddModelError(
                    string.Empty,
                    "Une erreur est survenue lors de la modification du pays.");

                await LoadRegionsForCountry(Input.RegionId);

                ViewBag.CountryId = id;

                return View(
                    "~/Views/Admin/Countries/Edit.cshtml",
                    Input);
            }
        }


        [HttpPost("Countries/Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCountry(int id)
        {
            var country = await _context.Countries
                .FirstOrDefaultAsync(c => c.Id == id);

            if (country == null)
            {
                return NotFound();
            }

            /*
             * On empêche la suppression si des cartes utilisent
             * encore ce pays.
             */
            var hasCards = await _context.Cards
                .AnyAsync(c => c.CountryId == id);

            if (hasCards)
            {
                TempData["ErrorMessage"] =
                    $"Impossible de supprimer le pays « {country.Name} » " +
                    "car des cartes lui sont encore associées.";

                return RedirectToAction(nameof(Countries));
            }

            var countryName = country.Name;

            try
            {
                _context.Countries.Remove(country);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"Le pays « {countryName} » a été supprimé avec succès.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Erreur lors de la suppression du pays {CountryId}.",
                    id);

                TempData["ErrorMessage"] =
                    $"Impossible de supprimer le pays « {countryName} ».";
            }

            return RedirectToAction(nameof(Countries));
        }

        private async Task LoadRegionsForCountry(int? selectedRegionId = null)
        {
            var regions = await _context.Regions
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .ToListAsync();

            ViewBag.Regions = new SelectList(
                regions,
                "Id",
                "Name",
                selectedRegionId);
        }

        private async Task CreateSpecializedCard(
    Card card,
    CreateCardViewModel input)
        {
            switch (input.Type)
            {
                case CardType.Leader:
                    _context.LeaderCards.Add(new LeaderCard
                    {
                        CardId = card.Id,
                        HealthPoints = input.HealthPoints!.Value
                    });
                    break;

                case CardType.Character:
                    _context.CharacterCards.Add(new CharacterCard
                    {
                        CardId = card.Id,
                        HealthPoints = input.HealthPoints!.Value
                    });
                    break;

                case CardType.PoliticalDecision:
                    _context.PoliticalDecisionCards.Add(
                        new PoliticalDecisionCard
                        {
                            CardId = card.Id
                        });
                    break;

                case CardType.Location:
                    _context.LocationCards.Add(
                        new LocationCard
                        {
                            CardId = card.Id
                        });
                    break;
            }

            await Task.CompletedTask;
        }

        private async Task UpdateSpecializedCard(
    Card card,
    CreateCardViewModel input)
        {
            var leader = await _context.LeaderCards
                .FirstOrDefaultAsync(x => x.CardId == card.Id);

            var character = await _context.CharacterCards
                .FirstOrDefaultAsync(x => x.CardId == card.Id);

            var decision = await _context.PoliticalDecisionCards
                .FirstOrDefaultAsync(x => x.CardId == card.Id);

            var location = await _context.LocationCards
                .FirstOrDefaultAsync(x => x.CardId == card.Id);

            if (input.Type != CardType.Leader && leader != null)
            {
                _context.LeaderCards.Remove(leader);
                leader = null;
            }

            if (input.Type != CardType.Character && character != null)
            {
                _context.CharacterCards.Remove(character);
                character = null;
            }

            if (input.Type != CardType.PoliticalDecision && decision != null)
            {
                _context.PoliticalDecisionCards.Remove(decision);
                decision = null;
            }

            if (input.Type != CardType.Location && location != null)
            {
                _context.LocationCards.Remove(location);
                location = null;
            }

            switch (input.Type)
            {
                case CardType.Leader:
                    if (leader == null)
                    {
                        _context.LeaderCards.Add(new LeaderCard
                        {
                            CardId = card.Id,
                            HealthPoints = input.HealthPoints!.Value
                        });
                    }
                    else
                    {
                        leader.HealthPoints =
                            input.HealthPoints!.Value;
                    }

                    break;

                case CardType.Character:
                    if (character == null)
                    {
                        _context.CharacterCards.Add(
                            new CharacterCard
                            {
                                CardId = card.Id,
                                HealthPoints = input.HealthPoints!.Value
                            });
                    }
                    else
                    {
                        character.HealthPoints =
                            input.HealthPoints!.Value;
                    }

                    break;

                case CardType.PoliticalDecision:
                    if (decision == null)
                    {
                        _context.PoliticalDecisionCards.Add(
                            new PoliticalDecisionCard
                            {
                                CardId = card.Id
                            });
                    }

                    break;

                case CardType.Location:
                    if (location == null)
                    {
                        _context.LocationCards.Add(
                            new LocationCard
                            {
                                CardId = card.Id
                            });
                    }

                    break;
            }
        }
        /// <summary>
        /// Synchronise les informations communes d'une variante
        /// avec sa carte principale.
        ///
        /// Les informations propres à la variante ne sont PAS modifiées :
        /// - Id
        /// - BaseCardId
        /// - VariantNumber
        /// - ArtType
        /// - ImageUrl
        /// - IsActive
        /// </summary>
        private static void SyncVariantSharedData(
            Card variant,
            Card baseCard)
        {
            variant.Name =
                baseCard.Name;

            variant.Type =
                baseCard.Type;

            variant.Rarity =
                baseCard.Rarity;

            variant.RegionId =
                baseCard.RegionId;

            variant.CountryId =
                baseCard.CountryId;

            variant.CardSetId =
                baseCard.CardSetId;

            variant.CollectionNumber =
                baseCard.CollectionNumber;

            variant.UsageCost =
                baseCard.UsageCost;

            /*
             * On garantit également la liaison.
             */
            variant.BaseCardId =
                baseCard.Id;
        }
    }
}
