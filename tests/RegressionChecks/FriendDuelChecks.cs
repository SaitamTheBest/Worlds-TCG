using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Effects;
using web_tcg.Services;

public static class FriendDuelChecks
{
    public static async Task RunAsync()
    {
        var connection=Environment.GetEnvironmentVariable("TCG_FRIENDS_TEST_CONNECTION")??throw new Exception("Base temporaire requise.");
        var services=new ServiceCollection();services.AddLogging();services.AddSignalR();services.AddOptions<GameRewardOptions>();
        services.AddDbContext<ApplicationDbContext>(o=>o.UseNpgsql(connection));
        services.AddScoped<NotificationService>();services.AddScoped<FriendsService>();services.AddScoped<CardBalanceRealtimeService>();
        services.AddScoped<FriendDuelService>();services.AddScoped<DeckValidationService>();services.AddScoped<GameService>();
        services.AddScoped<GameStateFactory>();services.AddScoped<GameStateRepository>();services.AddScoped<GameStateProjector>();services.AddScoped<GameAiService>();
        services.AddScoped<GameEngine>();services.AddScoped<GameConditionEvaluator>();services.AddScoped<GameEffectEngine>();services.AddScoped<GameTargetResolver>();
        services.AddSingleton<GameMatchLockProvider>();services.AddSingleton<MatchmakingService>();
        await using var provider=services.BuildServiceProvider();
        var suffix=Guid.NewGuid().ToString("N")[..8];var a="duel-a-"+suffix;var b="duel-b-"+suffix;var c="duel-c-"+suffix;
        Guid friendship=Guid.Empty, deckA=Guid.Empty,deckB=Guid.Empty;var passed=0;
        void Check(bool ok,string message) {if(!ok)throw new Exception(message);passed++;Console.WriteLine("PASS: "+message);}
        async Task<T> Read<T>(Func<ApplicationDbContext,Task<T>> fn) {await using var scope=provider.CreateAsyncScope();return await fn(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());}
        async Task<T> Act<T>(Func<FriendDuelService,Task<T>> fn) {await using var scope=provider.CreateAsyncScope();return await fn(scope.ServiceProvider.GetRequiredService<FriendDuelService>());}
        async Task Resolve(string user,Guid id,string action,Guid? deck=null) {await Act(async s=>{await s.ResolveAsync(user,id,action,deck);return true;});}
        async Task<bool> Reject(Func<Task> fn) {try {await fn();return false;}catch(Exception ex) when(ex is FriendRuleException or GameRuleException) {return true;}}
        await Read(async db=>{
            await db.Database.MigrateAsync();
            db.Users.AddRange(new ApplicationUser {Id=a,UserName=a},new ApplicationUser {Id=b,UserName=b},new ApplicationUser {Id=c,UserName=c});
            var relation=new Friendship {FirstUserId=a,SecondUserId=b,RequesterId=a,Status=FriendshipStatus.Accepted};db.Friendships.Add(relation);friendship=relation.Id;
            var set=new CardSet {Code=suffix,Name=suffix,ReleaseDate=DateTime.UtcNow,BoosterImageUrl="test"};var region=new Region {Code=suffix,Name=suffix};
            var leader=new Card {Name="Leader duel",Type=CardType.Leader,CollectionNumber=1,CardSet=set,Region=region};
            db.LeaderCards.Add(new() {Card=leader,HealthPoints=1000});
            var cards=Enumerable.Range(2,10).Select(i=>new Card {Name="Duel "+i,Type=CardType.Character,CollectionNumber=i,CardSet=set,Region=region}).ToList();
            foreach(var card in cards) db.CharacterCards.Add(new() {Card=card,HealthPoints=100});
            foreach(var owner in new[]{a,b}) {
                var deck=new Deck {UserId=owner,Name="Deck duel",LeaderCard=leader};db.Set<Deck>().Add(deck);
                if(owner==a)deckA=deck.Id;else deckB=deck.Id;
                db.UserCardCollections.Add(new() {UserId=owner,Card=leader,Quantity=1});
                foreach(var card in cards) {db.Set<DeckCard>().Add(new() {Deck=deck,Card=card,Quantity=4});db.UserCardCollections.Add(new() {UserId=owner,Card=card,Quantity=4});}
            }
            await db.SaveChangesAsync();return true;
        });
        Check(await Reject(async()=>{await Act(s=>s.InviteAsync(c,friendship,deckA));}),"duel : réservé aux amis acceptés");
        Check(await Reject(async()=>{await Act(s=>s.InviteAsync(a,friendship,deckB));}),"duel : deck de l'autre joueur refusé");
        var queue=provider.GetRequiredService<MatchmakingService>();await queue.TryEnqueueAndMatchAsync(a,deckA);
        Check(await Reject(async()=>{await Act(s=>s.InviteAsync(a,friendship,deckA));}),"duel : invitation impossible pendant recherche PVP");queue.Leave(a);
        var id=await Act(s=>s.InviteAsync(a,friendship,deckA));
        var pending=await Act(s=>s.GetAsync(id,b));
        Check(pending.Status==FriendDuelStatus.Pending && (pending.ExpiresAt-pending.CreatedAt).TotalMinutes is >4.9 and <5.1,
            "duel : invitation persistante avec expiration de cinq minutes");
        Check(await Read(db=>db.Notifications.AnyAsync(n=>n.UserId==b&&n.ActionUrl=="/Friends/Duel/"+id)),"duel : notification reçue par l'ami avec lien vers l'invitation");
        Check(await Reject(async()=>{await Act(s=>s.GetAsync(id,c));}),"duel : tiers ne peut consulter l'attente");
        Check(await Reject(()=>Resolve(a,id,"accept")) && await Reject(()=>Resolve(b,id,"start",deckB)),"duel : seul le destinataire accepte, deck avant acceptation refusé");
        Check(await Reject(async()=>{await Act(s=>s.InviteAsync(b,friendship,deckB));}),"duel : invitation croisée ou multiple refusée");
        await Read(db=>db.FriendDuelInvitations.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(-1))));
        Check(await Reject(()=>Resolve(b,id,"accept")) && (await Act(s=>s.GetAsync(id,a))).Status==FriendDuelStatus.Expired,
            "duel : réponse tardive refusée et invitation annulée automatiquement");
        var cancel=await Act(s=>s.InviteAsync(a,friendship,deckA));await Resolve(a,cancel,"cancel");
        Check((await Act(s=>s.GetAsync(cancel,b))).Status==FriendDuelStatus.Cancelled,"duel : invitant peut annuler l'attente");
        var decline=await Act(s=>s.InviteAsync(a,friendship,deckA));await Resolve(b,decline,"decline");
        Check((await Act(s=>s.GetAsync(decline,a))).Status==FriendDuelStatus.Declined,"duel : refus notifié et attente terminée");
        var unready=await Act(s=>s.InviteAsync(a,friendship,deckA));await Resolve(b,unready,"accept");
        await Read(db=>db.FriendDuelInvitations.Where(x=>x.Id==unready).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UtcNow.AddSeconds(-1))));
        await Act(s=>s.ExpireAsync());
        Check((await Act(s=>s.GetAsync(unready,a))).Status==FriendDuelStatus.Expired && await Reject(()=>Resolve(b,unready,"start",deckB)),
            "duel : choix du deck abandonné expire aussi automatiquement sans créer de partie");
        var chosen=await Act(s=>s.InviteAsync(a,friendship,deckA));await Resolve(b,chosen,"accept");
        Check((await Act(s=>s.GetAsync(chosen,a))).Status==FriendDuelStatus.Accepted && await Read(db=>db.Set<GameMatch>().CountAsync(x=>x.PlayerOneId==a||x.PlayerTwoId==a))==0,
            "duel : acceptation ouvre le choix du deck sans lancer de partie prématurément");
        Check(await Reject(()=>Resolve(b,chosen,"start",deckA)) && (await Act(s=>s.GetAsync(chosen,b))).Status==FriendDuelStatus.Accepted,
            "duel : deck adverse invalide ne crée pas de partie et préserve le choix");
        var lost=await Read(db=>db.UserCardCollections.Where(x=>x.UserId==a&&x.Card.Type==CardType.Character).Select(x=>x.Id).FirstAsync());
        await Read(db=>db.UserCardCollections.Where(x=>x.Id==lost).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Quantity,1)));
        Check(await Reject(()=>Resolve(b,chosen,"start",deckB)) && await Read(db=>db.Set<GameMatch>().CountAsync(x=>x.PlayerOneId==a||x.PlayerTwoId==a))==0,
            "duel : deck de l'invitant revalidé si sa collection change pendant l'attente");
        await Read(db=>db.UserCardCollections.Where(x=>x.Id==lost).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Quantity,4)));
        await Task.WhenAll(Resolve(b,chosen,"start",deckB),Resolve(b,chosen,"start",deckB));
        var started=await Act(s=>s.GetAsync(chosen,a));
        Check(started.Status==FriendDuelStatus.Started && started.MatchId.HasValue &&
            await Read(db=>db.Set<GameMatch>().CountAsync(x=>x.PlayerOneId==a||x.PlayerTwoId==a))==1,"duel : double confirmation simultanée crée une seule partie");
        Check(await Read(db=>db.Set<GameMatch>().AnyAsync(x=>x.Id==started.MatchId&&x.PlayerOneId==a&&x.PlayerTwoId==b&&x.Status==GameMatchStatus.InProgress&&x.StateJson!="{}")),
            "duel : vraie partie initialisée avec les deux joueurs et leurs decks");
        Check(await Read(db=>db.Notifications.CountAsync(n=>(n.UserId==a||n.UserId==b)&&n.ActionUrl=="/Game/Play/"+started.MatchId))==2,"duel : lien de partie notifié aux deux participants");
        Check(await Reject(async()=>{await Act(s=>s.InviteAsync(a,friendship,deckA));}),"duel : joueur déjà en partie ne peut inviter à nouveau");
        Console.WriteLine($"{passed} vérifications de duel réussies sur PostgreSQL.");
    }
}
