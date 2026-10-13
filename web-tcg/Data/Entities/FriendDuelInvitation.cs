namespace web_tcg.Data.Entities;

public enum FriendDuelStatus { Pending, Accepted, Started, Declined, Cancelled, Expired }

public sealed class FriendDuelInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FriendshipId { get; set; }
    public string InviterId { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public Guid InviterDeckId { get; set; }
    public Guid? RecipientDeckId { get; set; }
    public Guid? MatchId { get; set; }
    public FriendDuelStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);
}
