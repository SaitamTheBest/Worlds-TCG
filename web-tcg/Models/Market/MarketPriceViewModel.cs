namespace web_tcg.Models.Market;

public sealed class MarketPriceViewModel
{
    public int CardId { get; init; }
    public bool CanSell { get; set; }
    public decimal? AveragePrice { get; init; }
    public int SaleCount { get; init; }
    public List<MarketPricePoint> Points { get; init; } = new();
}

public sealed class MarketPricePoint
{
    public DateTime Date { get; init; }
    public decimal AveragePrice { get; init; }
    public int SaleCount { get; init; }
}
