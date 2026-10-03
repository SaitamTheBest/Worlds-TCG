using web_tcg.Data.Enums;
using web_tcg.Models.Card;

namespace web_tcg.Models.Admin
{
    /// <summary>
    /// Représente une entrée affichée dans le catalogue administrateur.
    /// Une entrée peut être une carte normale ou une variante graphique.
    /// </summary>
    public class AdminCardListItemViewModel
    {
        /// <summary>
        /// Identifiant de la carte principale.
        /// </summary>
        public int CardId { get; set; }

        /// <summary>
        /// Identifiant de la variante.
        /// Null pour la carte normale.
        /// </summary>
        public int? VariantId { get; set; }

        /// <summary>
        /// Numéro de variante.
        /// Null pour la carte normale.
        /// </summary>
        public int? VariantNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        public CardType Type { get; set; }

        public CardRarity? Rarity { get; set; }

        public CardArtType ArtType { get; set; }

        public int CollectionNumber { get; set; }

        public int CardSetId { get; set; }

        public string CardSetCode { get; set; } = string.Empty;

        public string CardSetName { get; set; } = string.Empty;

        public string RegionName { get; set; } = string.Empty;

        public string RegionColor { get; set; } = "#FFD700";

        /// <summary>
        /// ViewModel réellement envoyé au ViewComponent Card.
        /// </summary>
        public CardViewModel Card { get; set; } = null!;

        /// <summary>
        /// Indique si cette entrée représente une variante.
        /// </summary>
        public bool IsVariant => VariantId.HasValue;

        /// <summary>
        /// Numéro complet affiché dans le catalogue.
        ///
        /// Carte normale :
        /// P01-70
        ///
        /// Variante :
        /// P01-70-1
        /// </summary>
        public string SerialNumber =>
            VariantNumber.HasValue
                ? $"{CardSetCode}-{CollectionNumber:D2}-{VariantNumber.Value}"
                : $"{CardSetCode}-{CollectionNumber:D2}";
    }
}