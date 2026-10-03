using System.ComponentModel.DataAnnotations;
using web_tcg.Models.Card;

namespace web_tcg.Models.Profile
{
    public sealed class ProfileViewModel
    {
        public string Username { get; init; } = string.Empty;
        public string? AvatarUrl { get; init; }
        public decimal Balance { get; init; }
        public int XP { get; init; }
        public DateTime CreatedAt { get; init; }

        public IReadOnlyList<ProfileCardViewModel> ShowcaseCards { get; init; } =
            Array.Empty<ProfileCardViewModel>();

        public IReadOnlyList<ProfileCardViewModel> OwnedCards { get; init; } =
            Array.Empty<ProfileCardViewModel>();
    }

    public sealed class ProfileCardViewModel
    {
        /// <summary>
        /// ID de la carte physique exacte possédée.
        /// </summary>
        public int CardId { get; init; }

        /// <summary>
        /// Position dans la vitrine, null pour la collection.
        /// </summary>
        public int? Position { get; init; }

        /// <summary>
        /// Modèle utilisé directement par le ViewComponent Card.
        /// </summary>
        public CardViewModel Card { get; init; } = new();
    }

    public sealed class UpdateUsernameViewModel
    {
        [Required]
        [StringLength(30, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;
    }

    public sealed class UpdateProfileShowcaseViewModel
    {
        public List<int> CardIds { get; set; } = new();
    }
}