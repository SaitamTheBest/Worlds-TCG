using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace web_tcg.Data.Entities
{
    public class BoosterOpening
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Prix payé pour ce booster.
        /// </summary>
        [Range(0, 999999.99)]
        public decimal PricePaid { get; set; }

        /// <summary>
        /// Date à laquelle le booster a été ouvert.
        /// </summary>
        public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Cartes obtenues lors de l'ouverture.
        /// </summary>
        public ICollection<BoosterOpeningCard> Cards { get; set; } = new List<BoosterOpeningCard>();
    }
}
