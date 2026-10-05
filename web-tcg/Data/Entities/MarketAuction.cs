namespace web_tcg.Data.Entities;

public enum AuctionStatus { Active, Sold, Unsold, Cancelled }

public sealed class MarketAuction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;
    public string SellerId { get; set; } = string.Empty;
    public ApplicationUser Seller { get; set; } = null!;
    public decimal StartingPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public string? HighestBidderId { get; set; }
    public ApplicationUser? HighestBidder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime EndsAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public AuctionStatus Status { get; set; }
    public List<MarketBid> Bids { get; set; } = new();
}

public sealed class MarketBid
{
    public long Id { get; set; }
    public Guid AuctionId { get; set; }
    public MarketAuction Auction { get; set; } = null!;
    public string BidderId { get; set; } = string.Empty;
    public ApplicationUser Bidder { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
