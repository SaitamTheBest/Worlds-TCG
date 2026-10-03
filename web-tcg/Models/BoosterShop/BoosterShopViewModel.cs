using web_tcg.Data.Entities;

namespace web_tcg.Models.BoosterShop
{
    /// <summary>
    /// ViewModel utilisé pour afficher la page d'ouverture des boosters.
    /// </summary>
    public class BoosterShopViewModel
    {
        /// <summary>
        /// Liste des extensions disponibles,
        /// triées de la plus récente à la plus ancienne.
        /// </summary>
        public List<CardSet> CardSets { get; set; } = new();
    }
}
