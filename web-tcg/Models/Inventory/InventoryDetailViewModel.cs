using web_tcg.Data.Enums;
using web_tcg.Models.Card;

namespace web_tcg.Models.Inventory
{
    /// <summary>
    /// ViewModel de la page de détail d'une carte possédée.
    /// </summary>
    public class InventoryDetailViewModel
    {
        public int Id { get; set; }
        public int CardId { get; set; }

        // Informations liées à l'inventaire
        public int Quantity { get; set; }
        public DateTime AcquiredDate { get; set; }
        public string Condition { get; set; } = "Mint";
        public string? Notes { get; set; }
        public bool IsForSale { get; set; }
        public decimal? SalePrice { get; set; }

        // Informations complémentaires non présentes dans CardViewModel
        public string CardSetName { get; set; } = string.Empty;
        public DateTime CardSetReleaseDate { get; set; }
        public int? VariantNumber { get; set; }

        // Carte utilisée directement par le ViewComponent Card
        public CardViewModel Card { get; set; } = null!;
    }
}