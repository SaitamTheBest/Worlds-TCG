using System.ComponentModel.DataAnnotations;

namespace web_tcg.Data.Entities
{
    /// <summary>
    /// Carte représentant un personnage jouable.
    /// </summary>
    public class CharacterCard
    {
        public int Id { get; set; }

        public int CardId { get; set; }

        public Card Card { get; set; } = null!;

        /// <summary>
        /// Points de vie du personnage.
        /// </summary>
        [Range(10, 500)]
        public int HealthPoints { get; set; }
    }
}