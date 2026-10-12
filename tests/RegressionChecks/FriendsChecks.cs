using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Services;
using web_tcg.Models.Friends;

public static class FriendsChecks
{
    public static async Task RunAsync()
    {
        var connection=Environment.GetEnvironmentVariable("TCG_FRIENDS_TEST_CONNECTION")??throw new Exception("Base temporaire requise.");
        var services=new ServiceCollection(); services.AddLogging();services.AddSignalR();
        services.AddDbContext<ApplicationDbContext>(o=>o.UseNpgsql(connection));
        services.AddScoped<NotificationService>();services.AddScoped<FriendsService>();services.AddScoped<MarketService>();services.AddScoped<CardBalanceRealtimeService>();
        await using var provider=services.BuildServiceProvider();var suffix=Guid.NewGuid().ToString("N")[..8];
        var a="friend-a-"+suffix;var b="friend-b-"+suffix;var c="friend-c-"+suffix;var bot="friend-bot-"+suffix;int card,variant;var passed=0;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);passed++;Console.WriteLine("PASS: "+message);}
        async Task<T> Read<T>(Func<ApplicationDbContext,Task<T>> action){await using var scope=provider.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());}
        async Task<T> Act<T>(Func<FriendsService,Task<T>> action){await using var scope=provider.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<FriendsService>());}
        async Task Mutate(Func<FriendsService,Task> action){await Act(async service=>{await action(service);return true;});}
        async Task<bool> Reject(Func<FriendsService,Task> action){try{await Mutate(action);return false;}catch(FriendRuleException){return true;}}
        async Task<decimal> Money(string user)=>await Read(db=>db.Users.Where(x=>x.Id==user).Select(x=>x.CardBalance).SingleAsync());
        async Task<int> Quantity(string user,int id)=>await Read(db=>db.UserCardCollections.Where(x=>x.UserId==user&&x.CardId==id).SumAsync(x=>x.Quantity));
        await Read(async db=>{
            await db.Database.MigrateAsync();
            db.Users.AddRange(new ApplicationUser {Id=a,UserName=a,NormalizedUserName=a.ToUpperInvariant(),CardBalance=100},new ApplicationUser {Id=b,UserName=b,NormalizedUserName=b.ToUpperInvariant(),CardBalance=50},
                new ApplicationUser {Id=c,UserName=c,NormalizedUserName=c.ToUpperInvariant()},new ApplicationUser {Id=bot,UserName=bot,IsBot=true});
            var set=new CardSet {Name=suffix,Code=suffix,BoosterImageUrl="/test",ReleaseDate=DateTime.UtcNow};var region=new Region {Name=suffix,Code=suffix};
            var normal=new Card {Name="Carte amis",Type=CardType.Character,CardSet=set,Region=region,CollectionNumber=1};
            var alt=new Card {Name="Variante amis",Type=CardType.Character,CardSet=set,Region=region,CollectionNumber=1,BaseCard=normal,ArtType=CardArtType.FullArt,VariantNumber=1};
            db.Cards.AddRange(normal,alt);await db.SaveChangesAsync();
            db.UserCardCollections.AddRange(new() {UserId=a,CardId=normal.Id,Quantity=5},new() {UserId=b,CardId=alt.Id,Quantity=2});await db.SaveChangesAsync();return (normal.Id,alt.Id);
        });
        card=await Read(db=>db.Cards.Where(x=>x.CardSet.Code==suffix&&x.BaseCardId==null).Select(x=>x.Id).SingleAsync());
        variant=await Read(db=>db.Cards.Where(x=>x.CardSet.Code==suffix&&x.BaseCardId!=null).Select(x=>x.Id).SingleAsync());
        Check(await Reject(s=>s.RequestAsync(a,a))&&await Reject(s=>s.RequestAsync(a,bot)),"amis : invitations à soi-même et aux bots refusées");
        var requests=await Task.WhenAll(new[]{(a,b),(b,a)}.Select(async pair=>!await Reject(s=>s.RequestAsync(pair.Item1,pair.Item2))));
        Check(requests.Count(x=>x)==1,"amis : invitations croisées concurrentes créent une seule relation");
        var relation=await Read(db=>db.Friendships.AsNoTracking().SingleAsync(x=>x.FirstUserId==a&&x.SecondUserId==b));
        var sender=relation.RequesterId;var receiver=sender==a?b:a;
        Check(await Reject(s=>s.RespondAsync(sender,relation.Id,true))&&await Reject(s=>s.RespondAsync(c,relation.Id,true)),"amis : seul le destinataire peut accepter l'invitation");
        Check(await Reject(s=>s.MessageAsync(a,relation.Id,"avant acceptation")),"amis : messages interdits avant acceptation");
        await Mutate(s=>s.RespondAsync(receiver,relation.Id,true));
        Check(await Reject(s=>s.RespondAsync(receiver,relation.Id,true)),"amis : invitation acceptée une seule fois");
        await Mutate(s=>s.MessageAsync(a,relation.Id,"Bonjour <script>test</script>"));
        Check(await Read(db=>db.FriendMessages.CountAsync(x=>x.FriendshipId==relation.Id))==1&&await Read(db=>db.Notifications.AnyAsync(x=>x.UserId==b&&x.Title.StartsWith("Message de")&&x.ActionUrl=="/Friends/Conversation/"+relation.Id)),"amis : message conservé et destinataire notifié");
        Check(await Reject(s=>s.MessageAsync(c,relation.Id,"intrusion"))&&await Reject(s=>s.MessageAsync(a,relation.Id,new string('x',2001))),"amis : messages tiers et messages trop longs refusés");
        Check(await Reject(async s=>{await s.FriendshipAsync(relation.Id,c);}),"amis : conversation privée inaccessible à un tiers");
        FriendTradeInput Offer()=>new() {FriendshipId=relation.Id,OfferedMoney=20,RequestedMoney=5,Cards=new() {new() {CardId=card,Quantity=2,IsOffered=true},new() {CardId=variant,Quantity=1,IsOffered=false}}};
        Check(await Reject(async s=>{await s.ProposeAsync(c,Offer());}),"échanges : tiers exclu d'une relation d'amis");
        var invalid=Offer();invalid.OfferedMoney=-1;
        Check(await Reject(async s=>{await s.ProposeAsync(a,invalid);}),"échanges : montant négatif refusé");
        invalid=Offer();invalid.OfferedMoney=1.5m;
        Check(await Reject(async s=>{await s.ProposeAsync(a,invalid);}),"échanges : monnaie décimale refusée");
        invalid=Offer();invalid.Cards[0].Quantity=6;
        Check(await Reject(async s=>{await s.ProposeAsync(a,invalid);}),"échanges : exemplaires non possédés refusés");
        invalid=new() {FriendshipId=relation.Id};
        Check(await Reject(async s=>{await s.ProposeAsync(a,invalid);}),"échanges : proposition vide refusée");
        var trade=await Act(s=>s.ProposeAsync(a,Offer()));
        Check(await Money(a)==100&&await Quantity(a,card)==5,"échanges : proposition ne transfère ni monnaie ni carte");
        Check(await Read(db=>db.Notifications.CountAsync(x=>x.UserId==b&&x.Title=="Demande d'échange"&&x.ActionUrl=="/Friends/Trade/"+trade))==1,"échanges : demande notifiée une fois au destinataire");
        Check(await Reject(s=>s.ResolveAsync(a,trade,"accept"))&&await Reject(s=>s.ResolveAsync(c,trade,"accept")),"échanges : seul le destinataire confirme");
        var accepted=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>!await Reject(s=>s.ResolveAsync(b,trade,"accept"))));
        Check(accepted.Count(x=>x)==1&&await Money(a)==85&&await Money(b)==65,"échanges : double confirmation débite et crédite une seule fois");
        Check(await Quantity(a,card)==3&&await Quantity(b,card)==2&&await Quantity(a,variant)==1&&await Quantity(b,variant)==1,"échanges : cartes et variantes exactes transférées dans les deux sens");
        Check(await Read(db=>db.Notifications.CountAsync(x=>x.Title=="Échange confirmé"&&x.ActionUrl=="/Friends/Trade/"+trade))==2,"échanges : les deux amis reçoivent la confirmation");
        var stale=await Act(s=>s.ProposeAsync(a,new() {FriendshipId=relation.Id,Cards=new() {new() {CardId=card,Quantity=3,IsOffered=true}}}));
        Guid auction;
        await using(var scope=provider.CreateAsyncScope()){auction=await scope.ServiceProvider.GetRequiredService<MarketService>().CreateAsync(a,card,10,1);}
        Check(await Reject(s=>s.ResolveAsync(b,stale,"accept"))&&await Quantity(a,card)==2&&await Money(a)==85,"échanges : carte réservée au marché bloque l'acceptation sans paiement");
        await using(var scope=provider.CreateAsyncScope()){await scope.ServiceProvider.GetRequiredService<MarketService>().CancelAsync(auction,a);}
        var competing=await Act(s=>s.ProposeAsync(a,new() {FriendshipId=relation.Id,Cards=new() {new() {CardId=card,Quantity=3,IsOffered=true}}}));
        var completions=await Task.WhenAll(new[]{stale,competing}.Select(async id=>!await Reject(s=>s.ResolveAsync(b,id,"accept"))));
        Check(completions.Count(x=>x)==1&&await Quantity(a,card)==0&&await Quantity(b,card)==5,"échanges : deux propositions concurrentes ne dupliquent pas les cartes");
        var gift=await Act(s=>s.ProposeAsync(b,new() {FriendshipId=relation.Id,Cards=new() {new() {CardId=card,Quantity=1,IsOffered=true}}}));
        await Mutate(s=>s.ResolveAsync(a,gift,"accept"));
        var same=await Act(s=>s.ProposeAsync(a,new() {FriendshipId=relation.Id,Cards=new() {new() {CardId=card,Quantity=1,IsOffered=true},new() {CardId=card,Quantity=2,IsOffered=false}}}));
        await Mutate(s=>s.ResolveAsync(b,same,"accept"));
        Check(await Quantity(a,card)==2&&await Quantity(b,card)==3,"échanges : même carte des deux côtés conserve les exemplaires");
        var declined=await Act(s=>s.ProposeAsync(a,new() {FriendshipId=relation.Id,OfferedMoney=10}));await Mutate(s=>s.ResolveAsync(b,declined,"decline"));
        var cancelled=await Act(s=>s.ProposeAsync(a,new() {FriendshipId=relation.Id,OfferedMoney=10}));await Mutate(s=>s.ResolveAsync(a,cancelled,"cancel"));
        Check(await Money(a)==85&&await Money(b)==65&&await Reject(s=>s.ResolveAsync(b,cancelled,"accept")),"échanges : refus et annulation sans transfert, acceptation ultérieure refusée");
        var money=await Act(s=>s.ProposeAsync(a,new() {FriendshipId=relation.Id,OfferedMoney=10,RequestedMoney=5}));
        await Read(db=>db.Users.Where(x=>x.Id==b).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.CardBalance,0m)));
        Check(await Reject(s=>s.ResolveAsync(b,money,"accept"))&&await Money(a)==85,"échanges : solde devenu insuffisant bloque tous les transferts");
        Console.WriteLine($"{passed} vérifications amis et échanges réussies sur PostgreSQL.");
    }
}
