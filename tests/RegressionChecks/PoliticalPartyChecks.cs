using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using web_tcg.Controllers;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Models.Admin;
using web_tcg.Services;

public static class PoliticalPartyChecks
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TCG_PARTY_TEST_CONNECTION") ?? throw new Exception("Disposable database required.");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        void Check(bool ok,string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        var root = Path.Combine(Path.GetTempPath(),"tcg-party-checks-" + Guid.NewGuid().ToString("N"));
        var env = new PartyEnvironment { WebRootPath = root };
        var logo = "/uploads/parties/" + Guid.NewGuid().ToString("N") + ".png";
        Directory.CreateDirectory(Path.Combine(root,"uploads","parties"));
        await File.WriteAllBytesAsync(Path.Combine(root,logo.TrimStart('/')),new byte[] { 1 });
        try
        {
            var region = new Region { Name = "Region", Code = "R" + Guid.NewGuid().ToString("N")[..6] };
            var country = new Country { Name = "France test", Code = "FTA", Region = region };
            var other = new Country { Name = "Belgique test", Code = "BTA", Region = region };
            var set = new CardSet { Name = "Parties", Code = "PT", ReleaseDate = DateTime.UtcNow };
            db.AddRange(country,other,set); await db.SaveChangesAsync();
            PoliticalPartiesController Controller()
            {
                var http = new DefaultHttpContext();
                return new(db,env) { ControllerContext = new() { HttpContext = http }, TempData = new TempDataDictionary(http,new MemoryTempData()) };
            }
            PoliticalPartyViewModel Input() => new() { Name = "Parti test", CountryId = country.Id, ImageUrl = logo, Orientation = PoliticalOrientation.Left };
            Check(await Controller().Save(null,Input()) is RedirectToActionResult,"partis : migration et création avec pays, orientation et logo");
            var party = await db.PoliticalParties.SingleAsync();
            var view = (ViewResult)await Controller().Create(party.Id);
            Check(view.ViewName == "~/Views/Admin/PoliticalParties/Create.cshtml" && ((PoliticalPartyViewModel)view.Model!).ImageUrl == logo,
                "partis : modification utilise le formulaire de création prérempli");
            var controller = Controller();
            Check(await controller.Save(null,Input()) is ViewResult && !controller.ModelState.IsValid,"partis : doublon dans le même pays refusé");
            var edit = Input(); edit.Name = "Parti renommé"; edit.Orientation = PoliticalOrientation.Centre;
            Check(await Controller().Save(party.Id,edit) is RedirectToActionResult && party.Name == edit.Name && party.Orientation == PoliticalOrientation.Centre,
                "partis : nom et orientation modifiés sans recréer le parti");
            var card = new Card { Name = "Personnage", CardSetId = set.Id, RegionId = region.Id, CountryId = country.Id, Type = CardType.Character,
                CollectionNumber = 1, PoliticalPartyId = party.Id };
            var variant = new Card { Name = card.Name, CardSetId = set.Id, RegionId = region.Id, CountryId = country.Id, Type = CardType.Character,
                CollectionNumber = 1, PoliticalPartyId = party.Id, BaseCard = card, VariantNumber = 1, ArtType = CardArtType.FullArt };
            db.AddRange(card,variant); await db.SaveChangesAsync();
            var models = await new CardPresentationService(db).GetAsync(new[] {card.Id,variant.Id});
            Check(models.All(m => m.PoliticalPartyName == edit.Name && m.PoliticalPartyImageUrl == logo),"cartes : parti et logo transmis aux cartes normales et variantes");
            controller = Controller(); await controller.Delete(party.Id);
            Check(await db.PoliticalParties.AnyAsync(p=>p.Id == party.Id) && controller.TempData.ContainsKey("Error"),"partis : suppression interdite quand une carte utilise le parti");
            edit.CountryId = other.Id; controller = Controller();
            Check(await controller.Save(party.Id,edit) is ViewResult && !controller.ModelState.IsValid && party.CountryId == country.Id,
                "partis : changement de pays incompatible avec les cartes refusé");
            var admin = new AdminController(db,NullLogger<AdminController>.Instance);
            var input = new CreateCardViewModel { Id = card.Id, Name = card.Name, Type = card.Type, HealthPoints = 100, CardSetId = set.Id, RegionId = region.Id,
                CountryId = other.Id, CollectionNumber = 1, PoliticalPartyId = party.Id, MainImageUrl = "/uploads/cards/" + Guid.NewGuid().ToString("N") + ".png" };
            Check(await admin.CreateCard(card.Id,input) is ViewResult && admin.ModelState.ContainsKey(nameof(input.PoliticalPartyId)),"cartes : association à un parti d'un autre pays refusée");
            db.Cards.RemoveRange(variant,card); await db.SaveChangesAsync(); await Controller().Delete(party.Id);
            Check(!await db.PoliticalParties.AnyAsync(p=>p.Id == party.Id),"partis : suppression possible après retrait des associations");
        }
        finally { Directory.Delete(root,true); }
    }
    private sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string,object> LoadTempData(HttpContext context) => new Dictionary<string,object>();
        public void SaveTempData(HttpContext context,IDictionary<string,object> values) { }
    }
    private sealed class PartyEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "RegressionChecks";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

