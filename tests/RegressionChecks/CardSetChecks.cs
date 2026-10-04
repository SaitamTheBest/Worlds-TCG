using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using web_tcg.Controllers;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Models.Admin;

// Runs only against a disposable PostgreSQL instance supplied by the caller.
public static class CardSetChecks
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TCG_CARDSET_TEST_CONNECTION")
            ?? throw new InvalidOperationException("TCG_CARDSET_TEST_CONNECTION is required for the disposable test database.");
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var set = new CardSet { Name = "Extension " + suffix, Code = "T" + suffix, BoosterImageUrl = "/images/legacy.png", ReleaseDate = DateTime.UtcNow, BoosterPrice = 10 };
        var other = new CardSet { Name = "Autre " + suffix, Code = "A" + suffix, BoosterImageUrl = "/images/other.png", ReleaseDate = DateTime.UtcNow };
        context.CardSets.AddRange(set, other);
        await context.SaveChangesAsync();
        AdminController Controller()
        {
            var http = new DefaultHttpContext();
            return new AdminController(context, NullLogger<AdminController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
                TempData = new TempDataDictionary(http, new MemoryTempData())
            };
        }
        void Check(bool result, string name)
        {
            if (!result) throw new Exception(name);
            Console.WriteLine("PASS: " + name);
        }
        CreateCardSetViewModel Input() => new() { Name = set.Name, Code = set.Code, BoosterImageUrl = set.BoosterImageUrl, BoosterPrice = 25, IsActive = true, ReleaseDate = new DateTime(2026, 11, 1) };
        var controller = Controller();
        var view = await controller.EditCardSet(set.Id) as ViewResult;
        var model = view?.Model as CreateCardSetViewModel;
        Check(view?.ViewName == "~/Views/Admin/CardSets/Create.cshtml" && model?.Name == set.Name && model.BoosterImageUrl == set.BoosterImageUrl,
            "modification : formulaire commun prérempli, image existante conservée");
        controller = Controller();
        controller.ModelState.AddModelError(nameof(CreateCardSetViewModel.BoosterImageUrl), "ancienne URL incompatible avec le nouvel import");
        var result = await controller.EditCardSet(set.Id, Input());
        Check(result is RedirectToActionResult && controller.ModelState.IsValid, "modification : même nom/code acceptés et ancienne image conservée");
        await context.Entry(set).ReloadAsync();
        Check(set.BoosterPrice == 25 && set.IsActive && set.ReleaseDate == new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            "modification : prix, disponibilité et date enregistrés sur la même extension");
        Check(await context.CardSets.CountAsync(x => x.Id == set.Id) == 1, "modification : aucune nouvelle extension créée");
        controller = Controller();
        var duplicate = Input(); duplicate.Code = other.Code; duplicate.Name = other.Name; duplicate.BoosterPrice = 99;
        Check(await controller.EditCardSet(set.Id, duplicate) is ViewResult && controller.ModelState.ErrorCount == 2,
            "modification : nom et code d’une autre extension refusés");
        await context.Entry(set).ReloadAsync();
        Check(set.BoosterPrice == 25, "modification : validation en échec sans modification en base");
        controller = Controller();
        var invalid = Input(); invalid.BoosterImageUrl = "https://example.com/new.png";
        controller.ModelState.AddModelError(nameof(invalid.BoosterImageUrl), "import requis");
        Check(await controller.EditCardSet(set.Id, invalid) is ViewResult && !controller.ModelState.IsValid,
            "modification : une nouvelle URL externe ne bénéficie pas de l’exception pour l’image existante");
        Check(await Controller().EditCardSet(int.MaxValue) is NotFoundResult && await Controller().EditCardSet(int.MaxValue, Input()) is NotFoundResult,
            "modification : extension inexistante refusée en lecture et écriture");
    }

    private sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
