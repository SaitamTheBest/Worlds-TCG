using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_tcg.Controllers;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Models.Deck;

public static class SecretChecks
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TCG_SECRET_TEST_CONNECTION")
            ?? throw new InvalidOperationException("TCG_SECRET_TEST_CONNECTION doit pointer vers une base temporaire.");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        void Check(bool result, string name) { if (!result) throw new Exception(name); Console.WriteLine("PASS: " + name); }
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var region = new Region { Name = "SECRET test", Code = prefix };
        var secondRegion = new Region { Name = "Autre région", Code = "B" + prefix };
        var set = new CardSet { Name = "SECRET test", Code = prefix, ReleaseDate = DateTime.UtcNow };
        var user = new ApplicationUser { UserName = "secret-" + prefix };
        db.AddRange(region, secondRegion, set, user);
        await db.SaveChangesAsync();
        Card Secret(Region r) => new() { Name = "SECRET", CardSetId = set.Id, RegionId = r.Id,
            Type = CardType.Secret, Rarity = CardRarity.Gold, CollectionNumber = 0, IsActive = true };
        var secret = Secret(region);
        var leader = new Card { Name = "Leader", CardSetId = set.Id, RegionId = region.Id, Type = CardType.Leader, CollectionNumber = 1, IsActive = true };
        db.AddRange(secret, leader);
        await db.SaveChangesAsync();
        Check(secret.Id > 0, "SECRET : migration et insertion de la carte Gold 0");
        async Task<bool> Reject(Card card, string state)
        {
            db.Add(card);
            try { await db.SaveChangesAsync(); return false; }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException postgres && postgres.SqlState == state)
            { db.Entry(card).State = EntityState.Detached; return true; }
        }
        Check(await Reject(Secret(secondRegion), "23505"), "SECRET : doublon interdit même dans une autre région");
        var invalid = Secret(region); invalid.CollectionNumber = 2;
        Check(await Reject(invalid, "23514"), "SECRET : numéro autre que 0 interdit en base");
        invalid = Secret(region); invalid.Type = CardType.Character;
        Check(await Reject(invalid, "23514"), "SECRET : numéro 0 interdit aux cartes jouables");
        db.UserCardCollections.AddRange(new UserCardCollection { UserId = user.Id, CardId = secret.Id, Quantity = 1 },
            new UserCardCollection { UserId = user.Id, CardId = leader.Id, Quantity = 1 });
        await db.SaveChangesAsync();
        var controller = new DeckController(db) { ControllerContext = new ControllerContext
        { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier,user.Id) }, "test")) } } };
        var builder = (DeckBuilderDataDto)((JsonResult)await controller.GetBuilderData()).Value!;
        Check(builder.PlayableCards.Count == 0 && builder.Leaders.Single().CardId == leader.Id,
            "SECRET : carte possédée masquée dans le constructeur de deck");
        var result = await controller.Save(new SaveDeckRequest { Name = "Secret interdit", LeaderCardId = leader.Id,
            Cards = new() { new() { CardId = secret.Id, Quantity = 1 } } });
        Check(result is BadRequestObjectResult bad && bad.Value?.ToString()?.Contains("SECRET") == true,
            "SECRET : sauvegarde du deck refusée même en contournant le formulaire");
    }
}
