using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using web_tcg.Data.Enums;

namespace web_tcg.Data.Entities
{
    /// <summary>
    /// Représente une carte physique/collectionnable du TCG.
    ///
    /// Une carte normale possède BaseCardId = null.
    /// Une variante possède BaseCardId = l'Id de sa carte normale.
    ///
    /// Exemples :
    /// P01-70   => Id 10, BaseCardId null
    /// P01-70-1 => Id 11, BaseCardId 10
    /// P01-70-2 => Id 12, BaseCardId 10
    /// </summary>
    public class Card
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public CardType Type { get; set; }

        public CardRarity? Rarity { get; set; }

        public int RegionId { get; set; }

        public Region Region { get; set; } = null!;

        public int? CountryId { get; set; }

        public Country? Country { get; set; }
        public int? PoliticalPartyId { get; set; }
        public PoliticalParty? PoliticalParty { get; set; }

        public int CardSetId { get; set; }

        public CardSet CardSet { get; set; } = null!;

        /// <summary>
        /// Numéro principal.
        ///
        /// Exemple :
        /// 70 => P01-70
        ///
        /// Les variantes gardent également CollectionNumber = 70.
        /// </summary>
        public int CollectionNumber { get; set; }

        public int UsageCost { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        // =========================================================
        // VARIANTE
        // =========================================================

        /// <summary>
        /// Carte normale dont cette carte est une variante.
        ///
        /// Null = carte normale.
        /// Non null = variante.
        /// </summary>
        public int? BaseCardId { get; set; }

        /// <summary>
        /// Carte normale associée à cette variante.
        /// </summary>
        public Card? BaseCard { get; set; }

        /// <summary>
        /// Toutes les variantes de cette carte normale.
        ///
        /// Cette collection est utilisée essentiellement
        /// lorsque BaseCardId est null.
        /// </summary>
        public ICollection<Card> Variants { get; set; }
            = new List<Card>();

        /// <summary>
        /// Type d'illustration de cette carte précise.
        ///
        /// Normal pour la carte principale.
        /// FullArt / AlternateArt pour une variante.
        /// </summary>
        [Required]
        public CardArtType ArtType { get; set; }
            = CardArtType.Normal;

        /// <summary>
        /// Numéro de variante.
        ///
        /// Null pour la carte normale.
        ///
        /// 1 => P01-70-1
        /// 2 => P01-70-2
        /// </summary>
        public int? VariantNumber { get; set; }

        /// <summary>
        /// Permet de désactiver une impression
        /// sans la supprimer.
        /// </summary>
        public bool IsActive { get; set; } = true;

        [NotMapped]
        public bool IsVariant => BaseCardId.HasValue;

        // =========================================================
        // GAMEPLAY
        // =========================================================

        public ICollection<CardAbility> Abilities { get; set; }
            = new List<CardAbility>();
    }
}
