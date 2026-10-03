namespace web_tcg.Data.Entities
{
    /// <summary>
    /// Carte de décision politique.
    /// Elle produit un effet puis est défaussée.
    /// </summary>
    public class PoliticalDecisionCard
    {
        public int Id { get; set; }

        public int CardId { get; set; }

        public Card Card { get; set; } = null!;

        /// <summary>
        /// Indique qu'une décision est consommée après utilisation.
        /// Toujours true pour ce type de carte.
        /// </summary>
        public bool IsDiscardedAfterUse { get; set; } = true;
    }
}