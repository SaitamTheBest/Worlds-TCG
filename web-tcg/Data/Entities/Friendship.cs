using System.ComponentModel.DataAnnotations;
namespace web_tcg.Data.Entities;

public enum FriendshipStatus { Pending, Accepted, Declined }
public sealed class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstUserId { get; set; } = string.Empty;
    public ApplicationUser FirstUser { get; set; } = null!;
    public string SecondUserId { get; set; } = string.Empty;
    public ApplicationUser SecondUser { get; set; } = null!;
    public string RequesterId { get; set; } = string.Empty;
    public FriendshipStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class FriendMessage
{
    public long Id { get; set; }
    public Guid FriendshipId { get; set; }
    public Friendship Friendship { get; set; } = null!;
    public string SenderId { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum FriendTradeStatus { Pending, Completed, Declined, Cancelled }
public sealed class FriendTrade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FriendshipId { get; set; }
    public Friendship Friendship { get; set; } = null!;
    public string ProposerId { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public decimal OfferedMoney { get; set; }
    public decimal RequestedMoney { get; set; }
    public FriendTradeStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public List<FriendTradeCard> Cards { get; set; } = new();
}
public sealed class FriendTradeCard
{
    public long Id { get; set; }
    public Guid TradeId { get; set; }
    public FriendTrade Trade { get; set; } = null!;
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;
    public bool IsOffered { get; set; }
    public int Quantity { get; set; }
}
