using web_tcg.Data.Entities;
using web_tcg.Models.Home;
namespace web_tcg.Models.Friends;

public sealed class FriendDuelViewModel
{
    public string UserId { get; init; } = string.Empty;
    public Guid FriendshipId { get; init; }
    public string FriendName { get; init; } = string.Empty;
    public FriendDuelInvitation? Invitation { get; init; }
    public List<HomeDeckOption> Decks { get; init; } = new();
}
