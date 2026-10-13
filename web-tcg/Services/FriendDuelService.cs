using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Engine;

namespace web_tcg.Services;

public sealed class FriendDuelService(ApplicationDbContext db, FriendsService friends, DeckValidationService decks,
    GameService games, MatchmakingService matchmaking, NotificationService notifications, ILogger<FriendDuelService> logger)
{
    public const int LifetimeMinutes = 5;
    // One database lock serializes invitation creation/acceptance across app instances.
    private Task LockAsync(CancellationToken ct) => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(736248195)",ct);
    public Task<int> ExpireAsync(CancellationToken ct = default) => db.FriendDuelInvitations
        .Where(x => (x.Status == FriendDuelStatus.Pending || x.Status == FriendDuelStatus.Accepted) && x.ExpiresAt <= DateTime.UtcNow)
        .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status,FriendDuelStatus.Expired),ct);

    public async Task<FriendDuelInvitation> GetAsync(Guid id,string userId,CancellationToken ct = default)
    {
        await ExpireAsync(ct);
        return await db.FriendDuelInvitations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && (x.InviterId == userId || x.RecipientId == userId),ct)
            ?? throw new FriendRuleException("Invitation introuvable.");
    }
    private async Task AvailableAsync(string userId,CancellationToken ct)
    {
        if (matchmaking.IsQueued(userId) || await db.Set<GameMatch>().AnyAsync(x =>
            (x.PlayerOneId == userId || x.PlayerTwoId == userId) && (x.Status == GameMatchStatus.Starting || x.Status == GameMatchStatus.InProgress),ct))
            throw new FriendRuleException("Un des joueurs est déjà en partie ou en recherche PVP.");
    }
    public async Task<Guid> InviteAsync(string userId,Guid friendshipId,Guid deckId,CancellationToken ct = default)
    {
        var relation = await friends.FriendshipAsync(friendshipId,userId);
        var other = relation.FirstUserId == userId ? relation.SecondUserId : relation.FirstUserId;
        await decks.ValidateAsync(deckId,userId,ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        await ExpireAsync(ct);
        await AvailableAsync(userId,ct); await AvailableAsync(other,ct);
        if (await db.FriendDuelInvitations.AnyAsync(x => (x.Status == FriendDuelStatus.Pending || x.Status == FriendDuelStatus.Accepted) &&
            (x.InviterId == userId || x.RecipientId == userId || x.InviterId == other || x.RecipientId == other),ct))
            throw new FriendRuleException("Un des joueurs a déjà une invitation de duel en cours. Reprenez-la depuis Amis.");
        var invitation = new FriendDuelInvitation { FriendshipId=friendshipId,InviterId=userId,RecipientId=other,InviterDeckId=deckId,
            ExpiresAt=DateTime.UtcNow.AddMinutes(LifetimeMinutes) };
        db.FriendDuelInvitations.Add(invitation);
        var name = userId == relation.FirstUserId ? relation.FirstUser.UserName : relation.SecondUser.UserName;
        var note = notifications.AddPending(other,"Invitation en duel PVP",$"{name} vous invite en duel. Vous avez 5 minutes pour répondre.",NotificationType.Info,$"/Friends/Duel/{invitation.Id}");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await PublishAsync(note,ct);
        return invitation.Id;
    }
    public async Task ResolveAsync(string userId,Guid id,string action,Guid? deckId = null,CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var invitation = await db.FriendDuelInvitations.FirstOrDefaultAsync(x => x.Id == id && (x.InviterId == userId || x.RecipientId == userId),ct)
            ?? throw new FriendRuleException("Invitation introuvable.");
        if (invitation.Status == FriendDuelStatus.Started) {
            if (action == "start" && userId == invitation.RecipientId) return;
            throw new FriendRuleException("La partie a déjà commencé.");
        }
        if (invitation.Status is not (FriendDuelStatus.Pending or FriendDuelStatus.Accepted)) throw new FriendRuleException("Cette invitation est terminée.");
        if (invitation.ExpiresAt <= DateTime.UtcNow) {
            invitation.Status=FriendDuelStatus.Expired; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            throw new FriendRuleException("L’invitation a expiré après 5 minutes.");
        }
        var notes = new List<Notification>();
        switch (action)
        {
            case "cancel" when userId == invitation.InviterId:
                invitation.Status=FriendDuelStatus.Cancelled; break;
            case "decline" when userId == invitation.RecipientId:
                invitation.Status=FriendDuelStatus.Declined; break;
            case "accept" when userId == invitation.RecipientId && invitation.Status == FriendDuelStatus.Pending:
                await friends.FriendshipAsync(invitation.FriendshipId,userId);
                await AvailableAsync(invitation.InviterId,ct); await AvailableAsync(userId,ct);
                invitation.Status=FriendDuelStatus.Accepted;
                invitation.ExpiresAt=DateTime.UtcNow.AddMinutes(LifetimeMinutes);
                break;
            case "start" when userId == invitation.RecipientId && invitation.Status == FriendDuelStatus.Accepted && deckId.HasValue:
                await friends.FriendshipAsync(invitation.FriendshipId,userId);
                await AvailableAsync(invitation.InviterId,ct); await AvailableAsync(userId,ct);
                invitation.MatchId=await games.CreateMatchAsync(invitation.InviterId,invitation.InviterDeckId,userId,deckId.Value,ct);
                invitation.RecipientDeckId=deckId; invitation.Status=FriendDuelStatus.Started;
                foreach (var player in new[] { invitation.InviterId, invitation.RecipientId })
                    notes.Add(notifications.AddPending(player,"Votre duel commence","Les deux decks sont prêts. Rejoignez votre partie PVP.",NotificationType.Success,$"/Game/Play/{invitation.MatchId}"));
                break;
            default: throw new FriendRuleException("Cette action n’est pas autorisée.");
        }
        if (action != "start") notes.Add(notifications.AddPending(userId == invitation.InviterId ? invitation.RecipientId : invitation.InviterId,
            action == "accept" ? "Duel accepté" : "Invitation au duel terminée",
            action == "accept" ? "Votre ami choisit son deck. La partie commencera dès qu’il sera prêt." : "L’invitation au duel a été refusée ou annulée.",
            NotificationType.Info,$"/Friends/Duel/{id}"));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        foreach (var note in notes) await PublishAsync(note,ct);
    }
    private async Task PublishAsync(Notification note,CancellationToken ct) {
        try { await notifications.PublishAsync(note,ct); }
        catch (Exception ex) { logger.LogWarning(ex,"Notification de duel {Id} enregistrée, diffusion indisponible.",note.Id); }
    }
}
