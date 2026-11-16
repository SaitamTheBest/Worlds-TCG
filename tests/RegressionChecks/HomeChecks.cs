using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_tcg.Controllers;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Models.Home;

// Only run against the disposable regression database supplied by the caller.
public static class HomeChecks
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TCG_MARKET_TEST_CONNECTION")
            ?? throw new Exception("Base temporaire requise.");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var owner = "home-owner-" + suffix;
        var other = "home-other-" + suffix;
        var bot = "home-bot-" + suffix;
        var checks = 0;
        void Check(bool result, string name)
        {
            if (!result) throw new Exception(name);
            checks++;
            Console.WriteLine("PASS: " + name);
        }
        async Task<HomeViewModel> Read(string userId)
        {
            // Index does not use GameService; no game or external service is started.
            var controller = new HomeController(db, null!)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test"))
                } }
            };
            return (HomeViewModel)((ViewResult)await controller.Index()).Model!;
        }
        var before = await Read(owner);
        db.Users.AddRange(new ApplicationUser { Id = owner, UserName = owner },
            new ApplicationUser { Id = other, UserName = other },
            new ApplicationUser { Id = bot, UserName = bot, IsBot = true });
        var released = new CardSet { Name = "Home " + suffix, Code = suffix[..8], ReleaseDate = DateTime.UtcNow.AddDays(-1) };
        var future = new CardSet { Name = "Home future " + suffix, Code = suffix[..8] + "f", ReleaseDate = DateTime.UtcNow.AddDays(7) };
        var region = new Region { Name = "Home " + suffix, Code = suffix[..8] };
        Card Card(string name, int number, CardType type, CardSet set) => new()
        {
            Name = name, CollectionNumber = number, Type = type, CardSet = set, Region = region
        };
        var normal = Card("Personnage", 1, CardType.Character, released);
        var variant = Card("Personnage alternatif", 1, CardType.Character, released);
        variant.BaseCard = normal;
        variant.ArtType = CardArtType.AlternateArt;
        var leader = Card("Leader", 2, CardType.Leader, released);
        var inactive = Card("Carte désactivée", 3, CardType.Character, released); inactive.IsActive = false;
        var upcoming = Card("Carte future", 1, CardType.Character, future);
        db.Cards.AddRange(normal, variant, leader, inactive, upcoming);
        db.UserCardCollections.AddRange(
            new() { UserId = owner, Card = normal, Quantity = 5 },
            new() { UserId = owner, Card = variant, Quantity = 2 },
            new() { UserId = owner, Card = leader, Quantity = 0 },
            new() { UserId = owner, Card = inactive, Quantity = 100 },
            new() { UserId = owner, Card = upcoming, Quantity = 100 },
            new() { UserId = other, Card = leader, Quantity = 50 });
        db.BoosterOpenings.AddRange(new() { UserId = owner }, new() { UserId = bot });
        await db.SaveChangesAsync();
        var model = await Read(owner);
        Check(model.AvailableCards == before.AvailableCards + 3 && model.BaseCards == before.BaseCards + 2
            && model.Variants == before.Variants + 1 && model.AvailableCards == model.BaseCards + model.Variants,
            "Home : catalogue actif et sorti, variantes séparées, cartes futures et désactivées exclues");
        Check(model.CardSets == before.CardSets + 1, "Home : extensions futures exclues");
        Check(model.UserUniqueCards == 2 && model.UserTotalCards == 7,
            "Home : variantes uniques, quantités cumulées, aucune carte future, désactivée, à zéro ou d’un autre joueur");
        Check(model.CollectionProgress == Math.Round(200d / model.AvailableCards, 1),
            "Home : progression arrondie au dixième avec le même catalogue au numérateur et au dénominateur");
        Check(model.LeaderCards == before.LeaderCards + 1 && model.CharacterCards == before.CharacterCards + 1,
            "Home : types de cartes principales sans compter deux fois les variantes");
        Check(model.RegisteredPlayers == before.RegisteredPlayers + 2 && model.BoosterOpenings == before.BoosterOpenings + 1,
            "Home : joueurs et ouvertures réels, compte IA exclu");
        Check(model.UserDecks.Count == 0, "Home : aucun deck inventé");
        var empty = await Read(bot);
        Check(empty.UserUniqueCards == 0 && empty.UserTotalCards == 0 && empty.CollectionProgress == 0,
            "Home : collection vide à zéro sans division invalide");
        var secret = Card("Secrète", 0, CardType.Secret, released);
        secret.Rarity = CardRarity.Gold;
        db.Cards.Add(secret);
        await db.SaveChangesAsync();
        var withSecret = await Read(owner);
        Check(withSecret.SecretCards == model.SecretCards + 1 && withSecret.BaseCards == model.BaseCards + 1
            && withSecret.AvailableCards == model.AvailableCards + 1
            && withSecret.LeaderCards + withSecret.CharacterCards + withSecret.PoliticalDecisionCards
                + withSecret.LocationCards + withSecret.SecretCards == withSecret.BaseCards,
            "Home : cartes secrètes incluses et somme de tous les types égale au total des principales");
        Console.WriteLine($"{checks} vérifications des statistiques Home réussies sur PostgreSQL.");
    }
}
