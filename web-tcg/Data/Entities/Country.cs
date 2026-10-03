using System.ComponentModel.DataAnnotations;

namespace web_tcg.Data.Entities
{
    /// <summary>
    /// Pays pouvant être associé à une carte.
    /// </summary>
    public class Country
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(3)]
        public string Code { get; set; } = string.Empty;

        public int RegionId { get; set; }

        public Region Region { get; set; } = null!;

        public ICollection<Card> Cards { get; set; }
            = new List<Card>();
    }
}