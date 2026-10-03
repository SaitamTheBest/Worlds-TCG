using System.ComponentModel.DataAnnotations;

namespace web_tcg.Data.Entities
{
    /// <summary>
    /// Représente une extension du TCG.
    /// Une extension contient plusieurs cartes et possède son propre booster.
    /// </summary>
    public class CardSet
    {
        /// <summary>
        /// Identifiant unique de l'extension.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nom de l'extension.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Code court de l'extension.
        /// Exemple : P01, P02, EX01...
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;
        
        /// <summary>
        /// Indique si l'extension est active.
        /// </summary>
        public bool IsActive { get; set; } = false;

        /// <summary>
        /// Date de sortie officielle de l'extension.
        /// </summary>
        [Required]
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Chemin ou URL de l'image du booster.
        /// Exemple : /images/boosters/set-01.webp
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string BoosterImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Prix d'un booster dans la monnaie virtuelle du jeu.
        /// La monnaie n'est pas encore implémentée.
        /// </summary>
        [Required]
        [Range(0, 999999999)]
        public decimal BoosterPrice { get; set; }

        public ICollection<Card> Cards { get; set; }
            = new List<Card>();
    }
}