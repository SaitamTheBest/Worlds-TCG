using web_tcg.Models.Card;

namespace web_tcg.Models.Inventory
{
    /// <summary>
    /// ViewModel de la page d'inventaire.
    /// </summary>
    public class InventoryViewModel
    {
        public List<InventoryCardViewModel> Items { get; set; } = new();

        /// <summary>
        /// Nombre total de cartes possédées, doublons compris.
        /// </summary>
        public int TotalCards { get; set; }

        /// <summary>
        /// Nombre de cartes différentes possédées.
        /// </summary>
        public int UniqueCards { get; set; }

        public string? SearchQuery { get; set; }

        public string CurrentSort { get; set; } = "name";
    }

    /// <summary>
    /// Représente une carte affichée dans l'inventaire.
    /// </summary>
    public class InventoryCardViewModel
    {
        /// <summary>
        /// Identifiant de l'entrée dans UserCardCollection.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nombre d'exemplaires possédés.
        /// Cette valeur est uniquement affichée dans la vue.
        /// </summary>
        public int Quantity { get; set; }

        public DateTime AcquiredDate { get; set; }

        /// <summary>
        /// Modèle utilisé directement par le ViewComponent Card.
        /// </summary>
        public CardViewModel Card { get; set; } = null!;
    }
}