using System.ComponentModel.DataAnnotations;

namespace web_tcg.Data.Entities
{
    public class BoosterOpeningCard
    {
        public int Id { get; set; }

        [Required]
        public int BoosterOpeningId { get; set; }

        public BoosterOpening BoosterOpening { get; set; } = null!;

        /// <summary>
        /// Position de la carte dans le booster.
        /// </summary>
        [Range(1, 100)]
        public int Position { get; set; }

        [Required]
        public int CardId { get; set; }

        public Card Card { get; set; } = null!;

        /// <summary>
        /// Rareté déterminée lors du tirage.
        /// Permet de conserver le résultat exact du booster.
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string Rarity { get; set; } = string.Empty;

        /// <summary>
        /// Indique si la carte a déjà été révélée dans l'interface.
        /// </summary>
        public bool IsRevealed { get; set; } = false;
        public bool IsNew { get; set; }
    }
}
