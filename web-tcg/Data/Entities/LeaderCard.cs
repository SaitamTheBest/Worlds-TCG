using System.ComponentModel.DataAnnotations;

namespace web_tcg.Data.Entities
{
    /// <summary>
    /// Carte Leader / Président.
    /// Le Président représente la cible principale de la partie.
    /// </summary>
    public class LeaderCard
    {
        public int Id { get; set; }

        public int CardId { get; set; }

        public Card Card { get; set; } = null!;

        /// <summary>
        /// Nombre de points de vie du Président.
        /// </summary>
        [Range(500, 1500)]
        public int HealthPoints { get; set; }
    }
}