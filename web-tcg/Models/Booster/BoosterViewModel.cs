using web_tcg.Models.Card;

namespace web_tcg.Models.Booster
{
    public class BoosterOpeningViewModel
    {
        public int OpeningId { get; set; }
        public int CardSetId { get; set; }
        public DateTime OpenedAt { get; set; }
        public List<BoosterOpeningCardViewModel> Cards { get; set; } = [];
    }

    public class BoosterOpeningCardViewModel
    {
        public int Position { get; set; }
        public int CardId { get; set; }
        public string Rarity { get; set; } = string.Empty;
        public bool IsRevealed { get; set; }
        public bool IsNew { get; set; }
        public CardViewModel Card { get; set; } = new();
    }
}
