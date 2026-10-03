using System.ComponentModel.DataAnnotations;

namespace web_tcg.Data.Entities
{
    /// <summary>
    /// Représente une carte possédée par un utilisateur.
    /// Une variante étant une Card à part entière, CardId identifie
    /// directement la version exacte possédée.
    /// </summary>
    public class UserCardCollection
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Identifiant exact de la carte possédée.
        /// Peut pointer vers une carte normale ou une variante.
        /// </summary>
        [Required]
        public int CardId { get; set; }

        public Card Card { get; set; } = null!;

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;

        public DateTime AcquiredDate { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string Condition { get; set; } = "Mint";

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsForSale { get; set; } = false;

        [Range(0, 999999.99)]
        public decimal? SalePrice { get; set; }
    }
}