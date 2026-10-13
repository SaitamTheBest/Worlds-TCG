using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using web_tcg.Controllers;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Models.Inventory;
using web_tcg.Services;

public static class InventoryChecks
{
    sealed class TempProvider : ITempDataProvider {
        public IDictionary<string,object> LoadTempData(HttpContext context) => new Dictionary<string,object>();
        public void SaveTempData(HttpContext context,IDictionary<string,object> values) { }
    }
    public static async Task RunAsync()
    {
        var connection=Environment.GetEnvironmentVariable("TCG_MARKET_TEST_CONNECTION")??throw new Exception("Base temporaire requise.");
        var services=new ServiceCollection();services.AddLogging();services.AddSignalR();
        services.AddDbContext<ApplicationDbContext>(options=>options.UseNpgsql(connection));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<CardBalanceRealtimeService>();
        await using var provider=services.BuildServiceProvider();
        var userId="inventory-"+Guid.NewGuid().ToString("N");
        var checks=0;
        void Check(bool result,string message) { if(!result) throw new Exception(message);checks++;Console.WriteLine("PASS: "+message); }
        await using var scope=provider.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        db.Users.Add(new ApplicationUser { Id=userId,UserName=userId });
        var set=new CardSet { Name="Inventaire test "+userId[^8..],Code=userId[^8..],ReleaseDate=DateTime.UtcNow,BoosterImageUrl="test" };
        var region=new Region { Name="Inventaire test "+userId[^8..],Code=userId[^8..] };
        for(var i=1;i<=61;i++) db.UserCardCollections.Add(new() { UserId=userId,Quantity=2,
            Card=new Card { Name=$"Carte {i:00}",Type=CardType.Character,CollectionNumber=i,CardSet=set,Region=region } });
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        InventoryController Controller(string owner) {
            var context=new DefaultHttpContext { RequestServices=scope.ServiceProvider,
                User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,owner)},"test")) };
            var controller=new InventoryController(db,scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                scope.ServiceProvider.GetRequiredService<CardBalanceRealtimeService>(),scope.ServiceProvider.GetRequiredService<ILogger<InventoryController>>()) {
                ControllerContext=new ControllerContext { HttpContext=context, RouteData=new Microsoft.AspNetCore.Routing.RouteData() },TempData=new TempDataDictionary(context,new TempProvider()) };
            controller.Url=new UrlHelper(controller.ControllerContext);
            return controller;
        }
        var controller=Controller(userId);
        var model=(InventoryViewModel)((ViewResult)await controller.Index(page:2)).Model!;
        Check(model.Items.Count==11 && model.Page==2 && model.TotalPages==2 && model.UniqueCards==61 && model.TotalCards==122,
            "inventaire : 50 cartes par page, statistiques conservées sur toute la collection");
        var outside=(InventoryViewModel)((ViewResult)await controller.Index(page:99)).Model!;
        Check(outside.Page==2 && outside.Items.Count==11,"inventaire : page inexistante ramenée à la dernière page");
        var filtered=(InventoryViewModel)((ViewResult)await controller.Index(search:"Carte 60",page:2)).Model!;
        Check(filtered.Page==1 && filtered.Items.Count==1 && filtered.UniqueCards==1 && filtered.TotalCards==2,"inventaire : recherche appliquée avant pagination");
        var entry=await db.UserCardCollections.AsNoTracking().FirstAsync(x=>x.UserId==userId);
        var returnUrl="/Inventory?page=2&search=Carte&sortBy=quantity";
        var invalid=await controller.DiscardCard(entry.Id,3,returnUrl);
        Check(invalid is LocalRedirectResult redirect && redirect.Url==returnUrl && await db.Users.Where(x=>x.Id==userId).Select(x=>x.CardBalance).SingleAsync()==0,
            "inventaire : quantité excessive refusée, filtres conservés et aucun crédit");
        db.ChangeTracker.Clear();
        var result=await controller.DiscardCard(entry.Id,1,returnUrl);
        Check(result is LocalRedirectResult back && back.Url==returnUrl && await db.Users.Where(x=>x.Id==userId).Select(x=>x.CardBalance).SingleAsync()==1
            && await db.UserCardCollections.AnyAsync(x=>x.Id==entry.Id && x.Quantity==1),"inventaire : défausse depuis la liste, crédit exact et retour sur la même page");
        db.ChangeTracker.Clear();
        Check(await Controller("another-user").DiscardCard(entry.Id,1,returnUrl) is NotFoundObjectResult,"inventaire : impossible de défausser la carte d'un autre joueur");
        db.ChangeTracker.Clear();
        await controller.DiscardCard(entry.Id,1,returnUrl);
        Check(!await db.UserCardCollections.AnyAsync(x=>x.Id==entry.Id) && await db.Users.Where(x=>x.Id==userId).Select(x=>x.CardBalance).SingleAsync()==2,
            "inventaire : dernière copie retirée et monnaie créditée une seule fois");
        Check(await controller.DiscardCard(entry.Id,0,"https://example.org") is RedirectToActionResult,
            "inventaire : URL de retour externe refusée");
        db.ChangeTracker.Clear();
        var batch = await db.UserCardCollections.AsNoTracking().Where(x=>x.UserId==userId).Take(3)
            .Select(x=>new InventoryDiscardItem { Id=x.Id,Quantity=x.Quantity }).ToListAsync();
        var batchIds = batch.Select(x=>x.Id).ToList();
        await controller.DiscardSelection(new() { batch[0],batch[0] },returnUrl);
        Check(await db.UserCardCollections.CountAsync(x=>batchIds.Contains(x.Id))==3 && await db.Users.Where(x=>x.Id==userId).Select(x=>x.CardBalance).SingleAsync()==2,
            "sélection : doublons refusés sans défausse ni récompense");
        await Controller("another-user").DiscardSelection(batch,returnUrl);
        Check(await db.UserCardCollections.CountAsync(x=>batchIds.Contains(x.Id))==3,"sélection : cartes d'un autre joueur protégées");
        await controller.DiscardSelection(new() { batch[0],new() { Id=batch[1].Id,Quantity=1 } },returnUrl);
        Check(await db.UserCardCollections.CountAsync(x=>batchIds.Contains(x.Id))==3 && await db.Users.Where(x=>x.Id==userId).Select(x=>x.CardBalance).SingleAsync()==2,
            "sélection : quantité modifiée refuse le lot entier avant suppression");
        db.ChangeTracker.Clear();
        var bulkResult = await controller.DiscardSelection(batch,returnUrl);
        Check(bulkResult is LocalRedirectResult bulkBack && bulkBack.Url==returnUrl &&
            !await db.UserCardCollections.AnyAsync(x=>batchIds.Contains(x.Id)) &&
            await db.Users.Where(x=>x.Id==userId).Select(x=>x.CardBalance).SingleAsync()==8,
            "sélection : toutes les quantités supprimées ensemble et six exemplaires crédités");
        db.ChangeTracker.Clear();
        await controller.DiscardSelection(batch,returnUrl);
        Check(await db.Users.Where(x=>x.Id==userId).Select(x=>x.CardBalance).SingleAsync()==8,"sélection : répétition du POST ne crédite pas deux fois");
        Check(typeof(InventoryController).GetMethod(nameof(InventoryController.DiscardSelection))!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute),true),
            "sélection : endpoint protégé contre les requêtes sans jeton CSRF");
        Console.WriteLine($"{checks} vérifications inventaire réussies sur PostgreSQL.");
    }
}
