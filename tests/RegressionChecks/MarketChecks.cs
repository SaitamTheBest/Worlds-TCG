using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Services;

public static class MarketChecks
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TCG_MARKET_TEST_CONNECTION")
            ?? throw new InvalidOperationException("TCG_MARKET_TEST_CONNECTION doit pointer vers une base temporaire.");
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSignalR();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection));
        services.AddScoped<CardBalanceRealtimeService>(); services.AddScoped<MarketService>();
        services.AddScoped<NotificationService>();
        await using var provider=services.BuildServiceProvider();
        var prefix=Guid.NewGuid().ToString("N")[..8];
        var seller="seller-"+prefix; var alice="alice-"+prefix; var bob="bob-"+prefix;
        var passed=0;
        void Check(bool result,string message) { if(!result) throw new Exception(message); passed++; Console.WriteLine("PASS: "+message); }
        async Task<T> Read<T>(Func<ApplicationDbContext,Task<T>> action) { await using var scope=provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()); }
        async Task<T> Act<T>(Func<MarketService,Task<T>> action) { await using var scope=provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<MarketService>()); }
        async Task Mutate(Func<MarketService,Task> action) { await Act(async service => { await action(service); return true; }); }
        async Task<bool> Reject(Func<MarketService,Task> action) { try { await Mutate(action); return false; } catch(MarketRuleException) {return true;} }
        int cardId, variantId;
        await using(var scope=provider.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            db.Users.AddRange(new ApplicationUser {Id=seller,UserName=seller}, new ApplicationUser {Id=alice,UserName=alice,CardBalance=100},new ApplicationUser {Id=bob,UserName=bob,CardBalance=100});
            var set=new CardSet {Name="Marché test "+prefix,Code=prefix,BoosterImageUrl="/test.png",ReleaseDate=DateTime.UtcNow};
            var region=new Region {Name="Test",Code=prefix};
            var card=new Card {Name="Carte test",Type=CardType.Character,Rarity=CardRarity.Common,ArtType=CardArtType.Normal,Region=region,CardSet=set,CollectionNumber=1,IsActive=true};
            var variant=new Card {Name="Carte variante",Type=CardType.Character,Rarity=CardRarity.Common,ArtType=CardArtType.FullArt,BaseCard=card,VariantNumber=1,Region=region,CardSet=set,CollectionNumber=1,IsActive=true};
            db.Cards.AddRange(card,variant); await db.SaveChangesAsync(); cardId=card.Id;variantId=variant.Id;
            db.UserCardCollections.AddRange(new UserCardCollection {UserId=seller,CardId=cardId,Quantity=20},new UserCardCollection {UserId=seller,CardId=variantId,Quantity=1});
            await db.SaveChangesAsync();
        }
        async Task<decimal> Balance(string id) => await Read(db=>db.Users.Where(x=>x.Id==id).Select(x=>x.CardBalance).SingleAsync());
        async Task<int> Quantity(string id,int card) => await Read(db=>db.UserCardCollections.Where(x=>x.UserId==id&&x.CardId==card).SumAsync(x=>x.Quantity));
        async Task<web_tcg.Models.Market.MarketIndexViewModel> Purchases(string userId)
        {
            await using var scope=provider.CreateAsyncScope();
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var controller=new web_tcg.Controllers.MarketController(db,scope.ServiceProvider.GetRequiredService<MarketService>(),new CardPresentationService(db)) {
                ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext=new DefaultHttpContext {
                    User=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,userId) },"test")) } }
            };
            return (web_tcg.Models.Market.MarketIndexViewModel)((Microsoft.AspNetCore.Mvc.ViewResult)await controller.Purchases()).Model!;
        }
        var auction=await Act(service=>service.CreateAsync(seller,cardId,10,1));
        Check(await Reject(service=>service.CreateAsync(seller,cardId,10.5m,1)),"marché : prix de vente décimal refusé");
        Check(await Reject(service=>service.BidAsync(auction,alice,20.5m)) && await Balance(alice)==100,"marché : enchère décimale refusée sans débit");
        Check(await Quantity(seller,cardId)==19,"marché : une seule copie retirée de l’inventaire à la création");
        Check(await Reject(service=>service.BidAsync(auction,seller,10)),"marché : enchère sur sa propre vente refusée");
        await Mutate(service=>service.BidAsync(auction,alice,20));
        Check(await Balance(alice)==80,"marché : montant de l’enchère réservé");
        Check((await Purchases(alice)).Total==0,"marché : enchère active absente de l'historique des achats");
        await Mutate(service=>service.BidAsync(auction,alice,25));
        Check(await Read(db=>db.Notifications.CountAsync(x=>x.UserId==alice))==0,"marché : surenchère personnelle sans fausse notification");
        Check(await Balance(alice)==75,"marché : surenchère personnelle réserve uniquement la différence");
        await Mutate(service=>service.BidAsync(auction,bob,30));
        Check(await Balance(alice)==100&&await Balance(bob)==70,"marché : remboursement du précédent enchérisseur");
        Check(await Read(db=>db.Notifications.AnyAsync(x=>x.UserId==alice&&x.Title=="Votre enchère a été dépassée"&&x.Message.Contains("25")&&x.ActionUrl=="/Market/Auction/"+auction)),"marché : surenchère notifiée et remboursement indiqué");
        Check(await Reject(service=>service.BidAsync(auction,alice,101))&&await Balance(alice)==100,"marché : solde insuffisant refusé sans débit");
        Check(await Reject(service=>service.BidAsync(auction,alice,30)),"marché : enchère insuffisante ou obsolète refusée");
        Check(await Reject(service=>service.CancelAsync(auction,seller))&&await Reject(service=>service.CancelAsync(auction,alice)),"marché : annulation avec enchère ou par un autre joueur refusée");
        var bids=await Task.WhenAll(new[]{alice,bob}.Select(async id=>!await Reject(service=>service.BidAsync(auction,id,31))));
        Check(bids.Count(x=>x)==1,"marché : deux enchères simultanées, un seul gagnant au même montant");
        Check(await Balance(alice)+await Balance(bob)==169,"marché : crédits conservés après les enchères concurrentes");
        await Read(async db=>await db.MarketAuctions.Where(x=>x.Id==auction).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.EndsAt,DateTime.UtcNow.AddSeconds(-1))));
        Check(await Reject(service=>service.BidAsync(auction,alice,40)),"marché : enchère arrivée après expiration refusée");
        await Task.WhenAll(Mutate(service=>service.CloseExpiredAsync()),Mutate(service=>service.CloseExpiredAsync()));
        var sold=await Read(db=>db.MarketAuctions.AsNoTracking().SingleAsync(x=>x.Id==auction));
        Check(sold.Status==AuctionStatus.Sold&&await Balance(seller)==31&&await Quantity(sold.HighestBidderId!,cardId)==1,"marché : double clôture transfère la carte et paie le vendeur une seule fois");
        Check(await Read(db=>db.Notifications.CountAsync(x=>x.UserId==seller&&x.Title=="Carte vendue"&&x.ActionUrl=="/Market/Auction/"+auction))==1
            && await Read(db=>db.Notifications.CountAsync(x=>x.UserId==sold.HighestBidderId&&x.Title=="Carte achetée"&&x.ActionUrl=="/Market/Auction/"+auction))==1,"marché : double clôture notifie achat et vente une seule fois");
        var loser = sold.HighestBidderId == alice ? bob : alice;
        var purchases=await Purchases(sold.HighestBidderId!);
        Check(purchases.Mode=="purchases"&&purchases.Total==1&&purchases.Listings.Single().Auction.Id==auction,
            "marché : carte remportée présente dans l'historique de l'acheteur");
        Check((await Purchases(loser)).Total==0&&(await Purchases(seller)).Total==0,"marché : historique des achats limité au gagnant connecté");
        Check(await Read(db=>db.Notifications.CountAsync(x=>x.UserId==loser&&x.Title=="Enchère perdue"&&x.ActionUrl=="/Market/Auction/"+auction))==1,"marché : enchère perdue notifiée une fois à la clôture");
        Check(await Balance(alice)+await Balance(bob)+await Balance(seller)==200,"marché : total des crédits conservé après le paiement");
        var cancelled=await Act(service=>service.CreateAsync(seller,cardId,5,1));
        await Mutate(service=>service.CancelAsync(cancelled,seller));
        Check(await Quantity(seller,cardId)==19&&await Reject(service=>service.CancelAsync(cancelled,seller)),"marché : annulation rend la carte une seule fois");
        var unsold=await Act(service=>service.CreateAsync(seller,cardId,5,1));
        await Read(async db=>await db.MarketAuctions.Where(x=>x.Id==unsold).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.EndsAt,DateTime.UtcNow.AddSeconds(-1))));
        await Mutate(service=>service.CloseExpiredAsync());
        Check(await Quantity(seller,cardId)==19&&await Read(db=>db.MarketAuctions.AnyAsync(x=>x.Id==unsold&&x.Status==AuctionStatus.Unsold)),"marché : vente sans enchère terminée rend la carte");
        Check(await Read(db=>db.Notifications.CountAsync(x=>x.UserId==seller&&x.Title=="Carte vendue"))==1,"marché : annulation et invendu sans fausse notification de vente");
        var variants=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{try{return await Act(service=>service.CreateAsync(seller,variantId,10,1));}catch(MarketRuleException){return Guid.Empty;}}));
        Check(variants.Count(x=>x!=Guid.Empty)==1&&await Quantity(seller,variantId)==0,"marché : dernier exemplaire ne peut pas être mis en vente deux fois");
        await Mutate(service=>service.CancelAsync(variants.Single(x=>x!=Guid.Empty),seller));
        Check(await Quantity(seller,variantId)==1,"marché : variante exacte restituée sans conversion en carte de base");
        var limits=await Task.WhenAll(Enumerable.Range(0,12).Select(async _=>{try{return await Act(service=>service.CreateAsync(seller,cardId,10,1));}catch(MarketRuleException){return Guid.Empty;}}));
        Check(limits.Count(x=>x!=Guid.Empty)==10&&await Quantity(seller,cardId)==9,"marché : limite de dix ventes respectée même en création concurrente");
        Check(await Reject(service=>service.CreateAsync(seller,cardId,0,1))&&await Reject(service=>service.CreateAsync(seller,cardId,1,2)),"marché : prix et durée invalides refusés");
        var priceBefore=await Read(db=>new MarketPriceService(db).GetAsync(cardId));
        Check(priceBefore.SaleCount==1&&priceBefore.AveragePrice==31,"prix marché : seules les ventes conclues comptent, pas les enchères actives, invendues ou annulées");
        await Read(async db=> {
            foreach(var (card,price,date) in new[] { (cardId,10m,DateTime.UtcNow.Date.AddDays(-2)),(cardId,30m,DateTime.UtcNow.Date.AddDays(-2)),
                (cardId,50m,DateTime.UtcNow.Date.AddDays(-100)),(variantId,999m,DateTime.UtcNow.Date) })
                db.MarketAuctions.Add(new() { CardId=card,SellerId=seller,HighestBidderId=alice,StartingPrice=price,CurrentPrice=price,
                    Status=AuctionStatus.Sold,EndsAt=date,ClosedAt=date });
            await db.SaveChangesAsync(); return true;
        });
        var prices=await Read(db=>new MarketPriceService(db).GetAsync(cardId));
        Check(prices.SaleCount==4&&prices.AveragePrice==30.25m,"prix marché : moyenne pondérée par vente sur tout l'historique, variante exclue");
        Check(prices.Points.Count==2&&prices.Points.First().AveragePrice==20&&prices.Points.First().SaleCount==2&&prices.Points.Last().AveragePrice==31,
            "prix marché : courbe quotidienne chronologique limitée aux 90 derniers jours");
        var variantPrices=await Read(db=>new MarketPriceService(db).GetAsync(variantId));
        Check(variantPrices.SaleCount==1&&variantPrices.AveragePrice==999,"prix marché : historique indépendant de la variante");
        var emptyPrices=await Read(db=>new MarketPriceService(db).GetAsync(int.MaxValue));
        Check(emptyPrices.AveragePrice==null&&emptyPrices.Points.Count==0,"prix marché : absence de ventes sans faux prix à zéro");
        Console.WriteLine($"{passed} vérifications marché réussies sur PostgreSQL.");
    }
}
