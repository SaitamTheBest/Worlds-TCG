using System.ComponentModel.DataAnnotations;
using web_tcg.Data.Entities;
using web_tcg.Models.Card;

namespace web_tcg.Models.Market;

public sealed class MarketIndexViewModel
{
    public string Mode { get; init; } = "market";
    public string Search { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int Total { get; init; }
    public int ActiveSales { get; init; }
    public List<MarketListingViewModel> Listings { get; init; } = new();
    public string Title => Mode == "sales" ? "Mes ventes en cours" : Mode == "history" ? "Historique de mes ventes" : Mode == "purchases" ? "Historique de mes achats" : "Marché des joueurs";
}

public sealed class MarketListingViewModel
{
    public MarketAuction Auction { get; init; } = null!;
    public CardViewModel Card { get; init; } = null!;
    public bool IsSeller { get; init; }
    public bool IsHighestBidder { get; init; }
    public int BidCount { get; init; }
    public string Status => Auction.Status switch
    {
        AuctionStatus.Sold => "Vendue", AuctionStatus.Unsold => "Sans enchère",
        AuctionStatus.Cancelled => "Annulée", _ => Auction.EndsAt <= DateTime.UtcNow ? "Clôture en cours" : "En cours"
    };
    public decimal MinimumBid => Auction.HighestBidderId == null ? Auction.StartingPrice : Auction.CurrentPrice + 1;
}

public sealed class MarketDetailViewModel
{
    public MarketPriceViewModel MarketPrice { get; init; } = new();
    public MarketListingViewModel Listing { get; init; } = null!;
    public decimal AvailableBalance { get; init; }
    public List<MarketBid> Bids { get; init; } = new();
}

public sealed class CreateAuctionInput
{
    [Range(1, int.MaxValue)] public int CardId { get; set; }
    [Range(typeof(decimal), "1", "999999999", ErrorMessage="Le prix de départ doit être compris entre 1 et 999 999 999.")]
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(MarketPriceModelBinder))]
    [web_tcg.Models.Balance.WholeCurrency]
    public decimal StartingPrice { get; set; } = 1;
    public int DurationHours { get; set; } = 24;
    [Range(1, 2880)] public int? DurationMinutes { get; set; }
}

public sealed class CreateAuctionViewModel
{
    public CreateAuctionInput Input { get; init; } = new();
    public List<(CardViewModel Card, int Quantity)> Cards { get; init; } = new();
    public int ActiveSales { get; init; }
    public string Search { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public bool HasNext { get; init; }
}

public sealed class MarketBidInput { public decimal Amount { get; set; } }
