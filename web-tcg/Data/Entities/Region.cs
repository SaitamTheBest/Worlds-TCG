using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;

namespace web_tcg.Data.Entities
{
    /// <summary>
    /// Région ou faction à laquelle une carte est associée.
    /// Une région est notamment représentée par une couleur.
    /// </summary>
    public class Region
    {
        public int Id { get; set; }

        /// <summary>
        /// Nom de la région.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Code couleur hexadécimal utilisé par l'interface.
        /// Exemple : #F4C430.
        /// </summary>
        [Required]
        [MaxLength(7)]
        public string Color { get; set; } = "#FFFFFF";

        /// <summary>
        /// Code court permettant notamment d'identifier
        /// la région dans les numéros de cartes.
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        public ICollection<Country> Countries { get; set; }
            = new List<Country>();

        public ICollection<Card> Cards { get; set; }
            = new List<Card>();
    }
}