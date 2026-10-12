using System.ComponentModel.DataAnnotations;
using web_tcg.Data.Entities;
using web_tcg.Models.Card;
namespace web_tcg.Models.Friends;

public sealed class FriendsIndexViewModel
{
    public string UserId { get; init; } = string.Empty;
    public string Search { get; init; } = string.Empty;
    public List<ApplicationUser> Results { get; init; } = new();
    public List<Friendship> Relations { get; init; } = new();
}
public sealed class FriendConversationViewModel
{
    public string UserId { get; init; } = string.Empty;
    public Friendship Friendship { get; init; } = null!;
    public ApplicationUser Friend => Friendship.FirstUserId == UserId ? Friendship.SecondUser : Friendship.FirstUser;
    public List<FriendMessage> Messages { get; init; } = new();
    public List<FriendTrade> Trades { get; init; } = new();
}
public sealed class TradeCardInput
{
    public int CardId { get; set; }
    public bool IsOffered { get; set; }
    [Range(0,1000000)] public int Quantity { get; set; }
}
public sealed class FriendTradeInput
{
    public Guid FriendshipId { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(web_tcg.Models.Market.MarketPriceModelBinder))]
    [web_tcg.Models.Balance.WholeCurrency]
    [Range(typeof(decimal),"0","999999999")] public decimal OfferedMoney { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(web_tcg.Models.Market.MarketPriceModelBinder))]
    [web_tcg.Models.Balance.WholeCurrency]
    [Range(typeof(decimal),"0","999999999")] public decimal RequestedMoney { get; set; }
    public List<TradeCardInput> Cards { get; set; } = new();
}
public sealed class FriendTradeCreateViewModel
{
    public FriendTradeInput Input { get; init; } = new();
    public ApplicationUser Friend { get; init; } = null!;
    public decimal Balance { get; init; }
    public List<(CardViewModel Card,int Quantity)> OfferedCards { get; init; } = new();
    public List<(CardViewModel Card,int Quantity)> RequestedCards { get; init; } = new();
}
public sealed class FriendTradeViewModel
{
    public string UserId { get; init; } = string.Empty;
    public FriendTrade Trade { get; init; } = null!;
    public string ProposerName { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public List<(CardViewModel Card,int Quantity,bool IsOffered)> Cards { get; init; } = new();
}
