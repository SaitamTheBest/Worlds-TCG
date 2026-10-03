namespace web_tcg.Data.Entities
{
    /// <summary>
    /// Carte représentant un lieu présent sur le plateau.
    /// </summary>
    public class LocationCard
    {
        public int Id { get; set; }

        public int CardId { get; set; }

        public Card Card { get; set; } = null!;

        /// <summary>
        /// Indique que le lieu reste en jeu après avoir été posé.
        /// </summary>
        public bool RemainsInPlay { get; set; } = true;
    }
}