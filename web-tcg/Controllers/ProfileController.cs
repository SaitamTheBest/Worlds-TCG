using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Models.Card;
using web_tcg.Models.Profile;

namespace web_tcg.Controllers
{
    [Authorize]
    [Route("Profile")]
    public sealed class ProfileController : Controller
    {
        private const int MaxShowcaseCards = 3;
        private const long MaxAvatarSize = 4 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment environment,
            ILogger<ProfileController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            return View(await BuildViewModelAsync(user));
        }

        [HttpPost("username")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUsername(UpdateUsernameViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            if (!ModelState.IsValid)
            {
                TempData["ProfileError"] =
                    "Le pseudo doit contenir entre 3 et 30 caractères.";

                return RedirectToAction(nameof(Index));
            }

            var username = model.Username.Trim();

            if (string.Equals(user.UserName, username, StringComparison.Ordinal))
                return RedirectToAction(nameof(Index));

            var existingUser =
                await _userManager.FindByNameAsync(username);

            if (existingUser != null && existingUser.Id != user.Id)
            {
                TempData["ProfileError"] =
                    "Ce pseudo est déjà utilisé.";

                return RedirectToAction(nameof(Index));
            }

            var result =
                await _userManager.SetUserNameAsync(user, username);

            if (!result.Succeeded)
            {
                TempData["ProfileError"] =
                    string.Join(" ", result.Errors.Select(x => x.Description));

                return RedirectToAction(nameof(Index));
            }

            await _signInManager.RefreshSignInAsync(user);

            TempData["ProfileSuccess"] =
                "Votre pseudo a été modifié.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("avatar")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxAvatarSize + 1024 * 1024)]
        public async Task<IActionResult> UpdateAvatar(IFormFile? avatar)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            if (avatar == null || avatar.Length == 0)
            {
                TempData["ProfileError"] =
                    "Veuillez sélectionner une image.";

                return RedirectToAction(nameof(Index));
            }

            if (avatar.Length > MaxAvatarSize)
            {
                TempData["ProfileError"] =
                    "L'image ne doit pas dépasser 4 Mo.";

                return RedirectToAction(nameof(Index));
            }

            var extension =
                await DetectImageExtensionAsync(avatar);

            if (extension == null)
            {
                TempData["ProfileError"] =
                    "Format d'image invalide. Utilisez PNG, JPG ou WebP.";

                return RedirectToAction(nameof(Index));
            }

            var avatarDirectory =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "avatars");

            Directory.CreateDirectory(avatarDirectory);

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var filePath =
                Path.Combine(
                    avatarDirectory,
                    fileName);

            await using (var stream =
                new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
            {
                await avatar.CopyToAsync(stream);
            }

            DeletePreviousAvatar(
                user.AvatarUrl,
                avatarDirectory);

            user.AvatarUrl =
                $"/uploads/avatars/{fileName}";

            var result =
                await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                System.IO.File.Delete(filePath);

                TempData["ProfileError"] =
                    "Impossible de modifier l'avatar.";

                return RedirectToAction(nameof(Index));
            }

            TempData["ProfileSuccess"] =
                "Votre photo de profil a été modifiée.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("avatar/remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAvatar()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
            {
                var avatarDirectory =
                    Path.Combine(
                        _environment.WebRootPath,
                        "uploads",
                        "avatars");

                DeletePreviousAvatar(
                    user.AvatarUrl,
                    avatarDirectory);

                user.AvatarUrl = null;

                await _userManager.UpdateAsync(user);
            }

            TempData["ProfileSuccess"] =
                "Votre avatar a été supprimé.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("showcase")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateShowcase(
            UpdateProfileShowcaseViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var cardIds =
                (model.CardIds ?? new List<int>())
                .Where(x => x > 0)
                .ToList();

            if (cardIds.Count > MaxShowcaseCards)
            {
                TempData["ProfileError"] =
                    $"Vous pouvez afficher au maximum {MaxShowcaseCards} cartes.";

                return RedirectToAction(nameof(Index));
            }

            if (cardIds.Count != cardIds.Distinct().Count())
            {
                TempData["ProfileError"] =
                    "Une même carte ne peut apparaître qu'une seule fois.";

                return RedirectToAction(nameof(Index));
            }

            var ownedCardIds =
                await _context.UserCardCollections
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == user.Id &&
                        x.Quantity > 0 &&
                        cardIds.Contains(x.CardId))
                    .Select(x => x.CardId)
                    .Distinct()
                    .ToListAsync();

            if (ownedCardIds.Count != cardIds.Count)
            {
                TempData["ProfileError"] =
                    "Vous ne pouvez afficher que des cartes que vous possédez.";

                return RedirectToAction(nameof(Index));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.UserProfileShowcaseCards
                    .Where(x => x.UserId == user.Id)
                    .ExecuteDeleteAsync();

                for (var i = 0; i < cardIds.Count; i++)
                {
                    _context.UserProfileShowcaseCards.Add(
                        new UserProfileShowcaseCard
                        {
                            UserId = user.Id,
                            CardId = cardIds[i],
                            Position = i + 1,
                            AddedAt = DateTime.UtcNow
                        });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["ProfileSuccess"] =
                    "Votre vitrine a été mise à jour.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Erreur pendant la modification de la vitrine de {UserId}.",
                    user.Id);

                TempData["ProfileError"] =
                    "Impossible de modifier votre vitrine.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<ProfileViewModel> BuildViewModelAsync(ApplicationUser user)
        {
            var inventory = await _context.UserCardCollections
                .AsNoTracking()
                .Where(x => x.UserId == user.Id && x.Quantity > 0)
                .Include(x => x.Card)
                    .ThenInclude(x => x.Region)
                .Include(x => x.Card)
                    .ThenInclude(x => x.Country)
            .Include(x => x.Card.PoliticalParty)
                .Include(x => x.Card)
                    .ThenInclude(x => x.CardSet)
                .Include(x => x.Card)
                    .ThenInclude(x => x.Abilities)
                .Include(x => x.Card)
                    .ThenInclude(x => x.BaseCard)
                        .ThenInclude(x => x!.Abilities)
                .ToListAsync();

            var gameplayCardIds = inventory
                .Select(x => x.Card.BaseCardId ?? x.Card.Id)
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

            var ownedCards = inventory
                .Select(item => new ProfileCardViewModel
                {
                    CardId = item.CardId,
                    Card = BuildCardViewModel(
                        item.Card,
                        leaderHealthPoints,
                        characterHealthPoints)
                })
                .OrderBy(x => x.Card.Name)
                .ToList();

            var ownedById = ownedCards
                .ToDictionary(x => x.CardId);

            var showcaseRows = await _context.UserProfileShowcaseCards
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .OrderBy(x => x.Position)
                .ToListAsync();

            var showcaseCards = showcaseRows
                .Where(x => ownedById.ContainsKey(x.CardId))
                .Select(x => new ProfileCardViewModel
                {
                    CardId = x.CardId,
                    Position = x.Position,
                    Card = ownedById[x.CardId].Card
                })
                .ToList();

            return new ProfileViewModel
            {
                Username = user.UserName ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                Balance = user.Balance,
                XP = user.XP,
                CreatedAt = user.CreatedAt,
                ShowcaseCards = showcaseCards,
                OwnedCards = ownedCards
            };
        }

        private static CardViewModel BuildCardViewModel(
            Card physicalCard,
            IReadOnlyDictionary<int, int> leaderHealthPoints,
            IReadOnlyDictionary<int, int> characterHealthPoints)
        {
            // Pour une variante, les données de gameplay viennent de la carte de base.
            // L'illustration et l'ArtType viennent toujours de la carte physique possédée.
            var gameplayCard = physicalCard.BaseCard ?? physicalCard;
            var gameplayCardId = physicalCard.BaseCardId ?? physicalCard.Id;

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

            var collectionNumber = physicalCard.VariantNumber.HasValue
                ? $"{physicalCard.CollectionNumber}-{physicalCard.VariantNumber.Value}"
                : physicalCard.CollectionNumber.ToString();

            return new CardViewModel
            {
                Name = physicalCard.Name,
                Type = physicalCard.Type,
                Rarity = physicalCard.Rarity,
                UsageCost = physicalCard.UsageCost,
                HealthPoints = healthPoints,

                // Important : carte physique exacte.
                ImageUrl = physicalCard.ImageUrl,
                ArtType = physicalCard.ArtType,

                CollectionNumber = collectionNumber,

                RegionName = physicalCard.Region?.Name ?? "Région",
                PoliticalPartyName = physicalCard.PoliticalParty?.Name,
                PoliticalPartyOrientation = physicalCard.PoliticalParty?.Orientation,
                PoliticalPartyImageUrl = physicalCard.PoliticalParty?.ImageUrl,
                CountryName = physicalCard.Country?.Name ?? string.Empty,

                RegionColor = !string.IsNullOrWhiteSpace(physicalCard.Region?.Color)
                    ? physicalCard.Region.Color
                    : "#FFD700",

                CardSetCode = physicalCard.CardSet?.Code ?? string.Empty,

                // Important : gameplay de la carte de base.
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
        }

        private static async Task<string?> DetectImageExtensionAsync(
            IFormFile file)
        {
            var header = new byte[12];

            await using var stream =
                file.OpenReadStream();

            var read =
                await stream.ReadAsync(
                    header.AsMemory(0, header.Length));

            if (read >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A)
            {
                return ".png";
            }

            if (read >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF)
            {
                return ".jpg";
            }

            if (read >= 12 &&
                header[0] == 'R' &&
                header[1] == 'I' &&
                header[2] == 'F' &&
                header[3] == 'F' &&
                header[8] == 'W' &&
                header[9] == 'E' &&
                header[10] == 'B' &&
                header[11] == 'P')
            {
                return ".webp";
            }

            return null;
        }

        private static void DeletePreviousAvatar(
            string? avatarUrl,
            string avatarDirectory)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl))
                return;

            var fileName =
                Path.GetFileName(avatarUrl);

            if (string.IsNullOrWhiteSpace(fileName))
                return;

            var filePath =
                Path.Combine(
                    avatarDirectory,
                    fileName);

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }
}