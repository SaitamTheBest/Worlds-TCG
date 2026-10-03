using System.ComponentModel.DataAnnotations;

namespace web_tcg.Data.Entities
{
    public class UserProfileShowcaseCard
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        public int CardId { get; set; }

        public Card Card { get; set; } = null!;

        [Range(1, 3)]
        public int Position { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}