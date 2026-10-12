using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Hubs;
using web_tcg.Models.Friends;
namespace web_tcg.Services;

public sealed class FriendRuleException(string message) : Exception(message);
public sealed class FriendsService(ApplicationDbContext db, NotificationService notifications,
    IHubContext<FriendsHub> hub, CardBalanceRealtimeService balances, ILogger<FriendsService> logger)
{
    public async Task<Friendship> FriendshipAsync(Guid id,string userId)
    {
        var relation=await db.Friendships.AsNoTracking().Include(x=>x.FirstUser).Include(x=>x.SecondUser)
            .FirstOrDefaultAsync(x=>x.Id==id&&(x.FirstUserId==userId||x.SecondUserId==userId)&&x.Status==FriendshipStatus.Accepted);
        return relation ?? throw new FriendRuleException("Cette conversation est réservée aux deux amis.");
    }

    public async Task RequestAsync(string userId,string otherId)
    {
        if(userId==otherId) throw new FriendRuleException("Vous ne pouvez pas vous inviter vous-même.");
        await using var tx=await db.Database.BeginTransactionAsync(); await CollectionTransactionLock.AcquireAsync(db);
        var users=await db.Users.AsNoTracking().Where(x=>(x.Id==userId||x.Id==otherId)&&!x.IsBot).ToListAsync();
        if(users.Count!=2) throw new FriendRuleException("Joueur introuvable.");
        var first=string.CompareOrdinal(userId,otherId)<0?userId:otherId; var second=first==userId?otherId:userId;
        var relation=await db.Friendships.FirstOrDefaultAsync(x=>x.FirstUserId==first&&x.SecondUserId==second);
        if(relation?.Status is FriendshipStatus.Pending or FriendshipStatus.Accepted) throw new FriendRuleException("Vous êtes déjà amis ou une invitation est en attente.");
        if(relation==null) { relation=new() {FirstUserId=first,SecondUserId=second}; db.Friendships.Add(relation); }
        relation.RequesterId=userId; relation.Status=FriendshipStatus.Pending; relation.CreatedAt=DateTime.UtcNow;
        var note=notifications.AddPending(otherId,"Invitation d'ami",$"{users.First(x=>x.Id==userId).UserName} souhaite devenir votre ami.",NotificationType.Info,"/Friends");
        await CommitAsync(tx,new[]{note},userId,otherId);
    }

    public async Task RespondAsync(string userId,Guid id,bool accept)
    {
        await using var tx=await db.Database.BeginTransactionAsync(); await CollectionTransactionLock.AcquireAsync(db);
        var relation=await db.Friendships.FirstOrDefaultAsync(x=>x.Id==id&&(x.FirstUserId==userId||x.SecondUserId==userId));
        if(relation==null||relation.Status!=FriendshipStatus.Pending||relation.RequesterId==userId)
            throw new FriendRuleException("Cette invitation n'est plus disponible ou ne vous est pas destinée.");
        relation.Status=accept?FriendshipStatus.Accepted:FriendshipStatus.Declined;
        var notes=new List<Notification>();
        if(accept) {
            var name=await db.Users.Where(x=>x.Id==userId).Select(x=>x.UserName).SingleAsync();
            notes.Add(notifications.AddPending(relation.RequesterId,"Invitation acceptée",$"{name} a accepté votre invitation. Vous pouvez discuter et échanger des cartes.",NotificationType.Success,$"/Friends/Conversation/{id}"));
        }
        await CommitAsync(tx,notes,relation.FirstUserId,relation.SecondUserId);
    }

    public async Task MessageAsync(string userId,Guid id,string text)
    {
        text=(text??"").Trim(); if(text.Length is 0 or >2000) throw new FriendRuleException("Le message doit contenir entre 1 et 2 000 caractères.");
        await using var tx=await db.Database.BeginTransactionAsync(); await CollectionTransactionLock.AcquireAsync(db);
        var relation=await FriendshipAsync(id,userId); var friend=relation.FirstUserId==userId?relation.SecondUser:relation.FirstUser;
        var sender=relation.FirstUserId==userId?relation.FirstUser:relation.SecondUser;
        db.FriendMessages.Add(new() {FriendshipId=id,SenderId=userId,Text=text});
        var note=notifications.AddPending(friend.Id,$"Message de {sender.UserName}",text.Length>150?text[..150]+"…":text,
            NotificationType.Info,$"/Friends/Conversation/{id}");
        await CommitAsync(tx,new[]{note},userId,friend.Id);
    }

    public async Task<Guid> ProposeAsync(string userId,FriendTradeInput input)
    {
        ValidateMoney(input.OfferedMoney); ValidateMoney(input.RequestedMoney);
        if(input.Cards==null||input.Cards.Count>1000||input.Cards.Any(x=>x==null||x.CardId<=0||x.Quantity<0||x.Quantity>1000000))
            throw new FriendRuleException("Les quantités de cartes sont invalides (maximum : 100 cartes différentes).");
        var selected=input.Cards.Where(x=>x.Quantity>0).ToList();
        if(selected.Count>100) throw new FriendRuleException("Un échange peut contenir au maximum 100 cartes différentes.");
        if(selected.Select(x=>(x.CardId,x.IsOffered)).Distinct().Count()!=selected.Count) throw new FriendRuleException("Une carte ne peut apparaître qu'une fois par côté de l'échange.");
        if(selected.Count==0&&input.OfferedMoney==0&&input.RequestedMoney==0) throw new FriendRuleException("Ajoutez une carte ou de la monnaie à l'échange.");
        await using var tx=await db.Database.BeginTransactionAsync(); await CollectionTransactionLock.AcquireAsync(db);
        var relation=await FriendshipAsync(input.FriendshipId,userId); var other=relation.FirstUserId==userId?relation.SecondUserId:relation.FirstUserId;
        if(await db.FriendTrades.CountAsync(x=>x.ProposerId==userId&&x.Status==FriendTradeStatus.Pending)>=20)
            throw new FriendRuleException("Vous avez déjà 20 propositions en attente. Annulez-en une avant de continuer.");
        var trade=new FriendTrade {FriendshipId=relation.Id,ProposerId=userId,RecipientId=other,
            OfferedMoney=input.OfferedMoney,RequestedMoney=input.RequestedMoney,Cards=selected.Select(x=>new FriendTradeCard {CardId=x.CardId,Quantity=x.Quantity,IsOffered=x.IsOffered}).ToList()};
        await CheckAssetsAsync(trade);
        db.FriendTrades.Add(trade);
        var name=await db.Users.Where(x=>x.Id==userId).Select(x=>x.UserName).SingleAsync();
        var note=notifications.AddPending(other,"Demande d'échange",$"{name} vous propose un échange de cartes et de monnaie marché.",NotificationType.Info,$"/Friends/Trade/{trade.Id}");
        await CommitAsync(tx,new[]{note},userId,other); return trade.Id;
    }

    public async Task ResolveAsync(string userId,Guid id,string action)
    {
        if(action is not ("accept" or "decline" or "cancel")) throw new FriendRuleException("Action invalide.");
        await using var tx=await db.Database.BeginTransactionAsync(); await CollectionTransactionLock.AcquireAsync(db);
        var trade=await db.FriendTrades.Include(x=>x.Cards).FirstOrDefaultAsync(x=>x.Id==id&&(x.ProposerId==userId||x.RecipientId==userId));
        if(trade==null||trade.Status!=FriendTradeStatus.Pending) throw new FriendRuleException("Cet échange est introuvable ou déjà terminé.");
        if((action=="cancel"&&trade.ProposerId!=userId)||(action!="cancel"&&trade.RecipientId!=userId)) throw new FriendRuleException("Vous ne pouvez pas effectuer cette action sur cet échange.");
        await FriendshipAsync(trade.FriendshipId,userId);
        var notes=new List<Notification>();
        if(action=="accept") {
            await CheckAssetsAsync(trade);
            await TransferMoneyAsync(trade.ProposerId,trade.OfferedMoney,trade.RequestedMoney);
            await TransferMoneyAsync(trade.RecipientId,trade.RequestedMoney,trade.OfferedMoney);
            foreach(var owner in new[]{trade.ProposerId,trade.RecipientId}) {
                var proposer=owner==trade.ProposerId;
                foreach(var group in trade.Cards.GroupBy(x=>x.CardId)) {
                    var delta=group.Sum(x=>x.IsOffered==proposer?-x.Quantity:x.Quantity);
                    var owned=await db.UserCardCollections.FirstOrDefaultAsync(x=>x.UserId==owner&&x.CardId==group.Key);
                    var quantity=(long)(owned?.Quantity??0)+delta;
                    if(quantity<0||quantity>int.MaxValue) throw new FriendRuleException("Quantité d'exemplaires invalide.");
                    if(owned==null&&quantity>0) db.UserCardCollections.Add(new() {UserId=owner,CardId=group.Key,Quantity=(int)quantity});
                    else if(owned!=null) {
                        if(quantity==0) {db.UserCardCollections.Remove(owned); await db.UserProfileShowcaseCards.Where(x=>x.UserId==owner&&x.CardId==group.Key).ExecuteDeleteAsync();}
                        else owned.Quantity=(int)quantity;
                    }
                }
            }
            trade.Status=FriendTradeStatus.Completed;
            foreach(var owner in new[]{trade.ProposerId,trade.RecipientId})
                notes.Add(notifications.AddPending(owner,"Échange confirmé","L'échange a été conclu. Les cartes et la monnaie marché ont été transférées dans vos collections et soldes respectifs.",NotificationType.Success,$"/Friends/Trade/{id}"));
        } else {
            trade.Status=action=="cancel"?FriendTradeStatus.Cancelled:FriendTradeStatus.Declined;
            notes.Add(notifications.AddPending(userId==trade.ProposerId?trade.RecipientId:trade.ProposerId,
                action=="cancel"?"Échange annulé":"Échange refusé","La proposition est terminée. Aucune carte ni monnaie n'a été transférée.",NotificationType.Info,$"/Friends/Trade/{id}"));
        }
        trade.ClosedAt=DateTime.UtcNow;
        await CommitAsync(tx,notes,trade.ProposerId,trade.RecipientId);
        if(trade.Status==FriendTradeStatus.Completed) foreach(var owner in new[]{trade.ProposerId,trade.RecipientId}) {
            try { await balances.PublishAsync(owner,await db.Users.Where(x=>x.Id==owner).Select(x=>x.CardBalance).SingleAsync()); }
            catch(Exception ex) {logger.LogWarning(ex,"Solde de l'échange enregistré, diffusion indisponible");}
        }
    }

    private static void ValidateMoney(decimal amount) {
        if(amount<0||amount>999999999m||decimal.Truncate(amount)!=amount) throw new FriendRuleException("La monnaie doit être un entier compris entre 0 et 999 999 999.");
    }
    private async Task CheckAssetsAsync(FriendTrade trade) {
        foreach(var (owner,money,offered) in new[]{(trade.ProposerId,trade.OfferedMoney,true),(trade.RecipientId,trade.RequestedMoney,false)}) {
            if(!await db.Users.AnyAsync(x=>x.Id==owner&&!x.IsBot&&x.CardBalance>=money)) throw new FriendRuleException("L'un des joueurs n'a plus assez de monnaie marché disponible.");
            var entries=trade.Cards.Where(x=>x.IsOffered==offered).ToList(); var ids=entries.Select(x=>x.CardId).ToList();
            var owned=await db.UserCardCollections.AsNoTracking().Where(x=>x.UserId==owner&&ids.Contains(x.CardId)).ToDictionaryAsync(x=>x.CardId,x=>x.Quantity);
            if(entries.Any(x=>!owned.TryGetValue(x.CardId,out var quantity)||quantity<x.Quantity)) throw new FriendRuleException("L'un des joueurs ne possède plus les exemplaires disponibles pour cet échange. Les cartes en vente sont réservées au marché.");
        }
    }
    private async Task TransferMoneyAsync(string owner,decimal outgoing,decimal incoming) {
        if(await db.Users.Where(x=>x.Id==owner&&x.CardBalance>=outgoing)
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.CardBalance,x=>x.CardBalance-outgoing+incoming))!=1) throw new FriendRuleException("Le solde marché a changé. Aucun transfert n'a été effectué.");
    }
    private async Task CommitAsync(IDbContextTransaction tx,IEnumerable<Notification> notes,params string[] users) {
        await db.SaveChangesAsync(); await tx.CommitAsync();
        foreach(var note in notes) try {await notifications.PublishAsync(note);} catch(Exception ex) {logger.LogWarning(ex,"Notification d'ami {Id} enregistrée, diffusion indisponible",note.Id);}
        try {await hub.Clients.Users(users).SendAsync("FriendsChanged");} catch(Exception ex) {logger.LogWarning(ex,"Modification des amis enregistrée, diffusion indisponible");}
    }
}
