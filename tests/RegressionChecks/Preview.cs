using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using web_tcg.Data.Enums;
using web_tcg.Models.Card;
using web_tcg.Models.Deck;
using web_tcg.Models.Game;
using web_tcg.Models.Home;
using web_tcg.ViewComponents;

// Serveur de contrôle visuel : vues réelles, données fictives, aucune connexion BDD.
public static class Preview
{
    public static readonly Guid DeckId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static CardViewModel Card(int id) => new()
    {
        Id = id, Name = id == 1 ? "Président de démonstration" : "Personnage de démonstration",
        Type = id == 6 ? CardType.Secret : id == 1 ? CardType.Leader : CardType.Character,
        ArtType = id == 4 ? CardArtType.FullArt : id == 3 ? CardArtType.AlternateArt : CardArtType.Normal,
        Rarity = CardRarity.Rare, RegionColor = "#18467d", RegionName = "Région test",
        CollectionNumber = id.ToString(), CardSetCode = "TEST", UsageCost = 2, HealthPoints = 1200,
        Abilities = new()
        {
            new() { Type = AbilityType.Skill, Name = "Frappe", Description = "Inflige 40 dégâts à un personnage.", UsageCost = 10, DisplayOrder = 0 },
            new() { Type = AbilityType.Trait, Name = "Médiapart", Description = "Tous les deux tours, vous pouvez soigner ce personnage de 40 dégâts.", DisplayOrder = 1 },
            new() { Type = AbilityType.Effect, Name = "Bouclier", Description = "Protège de la première attaque.", DisplayOrder = 2 }
        }
    };
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder();
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../web-tcg/wwwroot"));
        builder.Environment.WebRootPath = webRoot;
        builder.Environment.WebRootFileProvider = new PhysicalFileProvider(webRoot);
        builder.WebHost.UseUrls("http://127.0.0.1:5299");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(CardViewComponent).Assembly)
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new PreviewControllersOnly()));
        builder.Services.AddSignalR();
        var app = builder.Build();
        app.UseExceptionHandler("/Error/500");
        app.UseStatusCodePagesWithReExecute("/Error/{0}");
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(webRoot) });
        app.MapControllers();
        app.MapControllerRoute("preview-links", "{controller}/{action}/{id?}");
        app.MapHub<PreviewLobbyHub>("/hubs/lobby");
        app.MapHub<PreviewGameHub>("/hubs/game");
        await app.RunAsync();
    }
}

public sealed class PreviewControllersOnly : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var controller in feature.Controllers.Where(c => c.AsType() != typeof(PreviewController)
            && c.AsType() != typeof(web_tcg.Controllers.ErrorController)).ToArray())
            feature.Controllers.Remove(controller);
    }
}

public sealed class PreviewController : Controller
{
    [HttpGet("/Admin/Cards")]
    public IActionResult AdminCards(int? cardSetId, string? rarity, int page = 1)
    {
        var items = Enumerable.Range(1, 30).Select(id => new web_tcg.Models.Admin.AdminCardListItemViewModel
        {
            CardId = id, Name = "Carte de démonstration " + id, CollectionNumber = id,
            CardSetId = id <= 18 ? 1 : 2, CardSetCode = id <= 18 ? "P01" : "P02", CardSetName = "Extension de démonstration",
            Rarity = id % 2 == 0 ? CardRarity.Rare : CardRarity.UltraRare, Type = CardType.Character,
            VariantId = id % 3 == 0 ? id : null, VariantNumber = id % 3 == 0 ? 1 : null,
            Card = Preview.Card(id)
        }).Where(x => !cardSetId.HasValue || x.CardSetId == cardSetId)
          .Where(x => string.IsNullOrEmpty(rarity) || rarity == ((int?)x.Rarity)?.ToString()).ToList();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(items.Count / 12d)));
        return View("~/Views/Admin/Cards/Index.cshtml", new web_tcg.Models.Admin.AdminCardIndexViewModel
        {
            Items = items.Skip((page - 1) * 12).Take(12).ToList(), Total = items.Count,
            BaseCardCount = items.Count(x => !x.IsVariant), Page = page, CardSetId = cardSetId, Rarity = rarity ?? "",
            CardSets = new() { new("P01 — Démonstration", "1"), new("P02 — Démonstration", "2") }
        });
    }
    [HttpGet("/__preview/error")] public IActionResult ServerError() => throw new InvalidOperationException("Erreur de contrôle interne, à ne pas afficher.");
    private static HomeViewModel Home => new()
    {
        AvailableCards = 120, BaseCards = 90, Variants = 30, RegisteredPlayers = 12, BoosterOpenings = 45,
        UserUniqueCards = 15, UserTotalCards = 48, CollectionProgress = 12.5,
        UserDecks = new[] { new HomeDeckOption { Id = Preview.DeckId, Name = "Deck de démonstration" } }
    };
    private static DeckSummaryDto Summary => new() { Id = Preview.DeckId, Name = "Deck de démonstration", LeaderName = "Président", LeaderCardId = 1, TotalCards = 3 };
    [HttpGet("/")] public IActionResult HomeView() => View("~/Views/Home/Index.cshtml", Home);
    [HttpGet("/Deck/Manage")] public IActionResult Decks() => View("~/Views/Deck/Manage.cshtml", new List<DeckSummaryDto> { Summary });
    [HttpGet("/Pvp/Play")] public IActionResult Pvp() => View("~/Views/Pvp/Play.cshtml", Home);
    [HttpGet("/Pvp/Activity")] public IActionResult Activity() => Json(new { playersInGame = 8, playersInQueue = 3 });
    [HttpGet("/Admin/CreateCard")] public IActionResult CreateCard()
    {
        Microsoft.AspNetCore.Mvc.Rendering.SelectList Options<T>() where T : Enum => new(Enum.GetValues(typeof(T)).Cast<T>().Select(value => new { Value = Convert.ToInt32(value), Text = value.ToString() }), "Value", "Text");
        ViewBag.CardTypes = Options<CardType>(); ViewBag.CardRarities = Options<CardRarity>();
        ViewBag.CardArtTypes = Options<CardArtType>(); ViewBag.AbilityTypes = Options<AbilityType>();
        var choices = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(new[] { new { Id = 1, Name = "Démonstration" } }, "Id", "Name");
        ViewBag.CardSets = choices; ViewBag.Regions = choices; ViewBag.Countries = choices;
        ViewBag.Regions = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(new[] { new { Id = 1, Name = "Bretagne" },new { Id = 2, Name = "Normandie" },new { Id = 3, Name = "Île-de-France" } },"Id","Name");
        ViewBag.Countries = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(new[] { new { Id = 10, Name = "France" },new { Id = 20, Name = "Belgique" } },"Id","Name");
        return View("~/Views/Admin/Cards/Create.cshtml", new web_tcg.Models.Admin.CreateCardViewModel { Type = CardType.Character, UsageCost = 20, HealthPoints = 100,
            Abilities = new() { new() { Type = AbilityType.Effect, Name = "Ralliement régional", Description = "+20 dégâts par personnage de Bretagne ou Normandie.",
                Effects = new() { new() { Type = GameEffectType.DamageBonus, Trigger = GameEffectTrigger.Passive, Target = GameTargetType.Self, Value = 20, ScaleByMatchingCharacters = true, CountRegionIds = new() {1,2} } },
                Conditions = new() { new() { Type = GameConditionType.SourceRegion, RegionIds = new() {1,2} } } } } });
    }
    [HttpGet("/Game/{id:guid}")] public IActionResult Game(Guid id) => View("~/Views/Game/Play.cshtml", new GameViewModel { MatchId = id, VisibleCards = new[] { Preview.Card(1), Preview.Card(2), Preview.Card(3) } });
    [HttpGet("/Card/Render/{id:int}")]
    public IActionResult Render(int id, string? image = null)
    {
        var model = Preview.Card(id);
        if (image?.StartsWith('/') == true && !image.StartsWith("//")) model.ImageUrl = image;
        return ViewComponent("Card", new { model });
    }
    [HttpGet("/Deck/MyDecks")] public IActionResult DeckList() => Json(new[] { Summary });
    [HttpGet("/Deck/{id:guid}")] public IActionResult Deck(Guid id) => Json(new DeckDetailDto { Id = id, Name = Summary.Name, LeaderCardId = 1, Cards = new() { new() { CardId = 2, Quantity = 1 }, new() { CardId = 3, Quantity = 2 } } });
    [HttpGet("/Deck/BuilderData")] public IActionResult Builder() => Json(new DeckBuilderDataDto
    {
        Leaders = new() { new() { CardId = 1, GameplayCardId = 1, Name = Preview.Card(1).Name, Type = CardType.Leader, OwnedQuantity = 1, ArtType = CardArtType.Normal } },
        PlayableCards = new() { new() { CardId = 2, GameplayCardId = 2, Name = Preview.Card(2).Name, OwnedQuantity = 1, ArtType = CardArtType.Normal }, new() { CardId = 3, GameplayCardId = 2, Name = Preview.Card(2).Name, OwnedQuantity = 4, ArtType = CardArtType.AlternateArt } }
    });
    [HttpPost("/Pvp/PlayVsIA")] public IActionResult Ai() => BadRequest("Le deck doit contenir exactement 40 cartes. Actuellement : 3.");
}

public sealed class PreviewLobbyHub : Hub
{
    public Task JoinQueue(Guid deckId) => throw new HubException("Le deck doit contenir exactement 40 cartes. Actuellement : 3.");
    public Task LeaveQueue() => Task.CompletedTask;
}
public sealed class PreviewGameHub : Hub
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, GameStateDto> States = new();
    public async Task JoinGame(Guid matchId)
    {
        var demoTargets = new List<Guid>();
        GameCardDto Card(int id) => new() { InstanceId = Guid.NewGuid(), CardId = id, Name = Preview.Card(id).Name, Type = Preview.Card(id).Type, MaxHealth = 1200, CurrentHealth = 1000, UsageCost = 2,
            Abilities = new[] { new GameAbilityDto { Id = 7, Type = AbilityType.Skill, Name = "Attaque de démonstration", Description = "Inflige 20 dégâts à une unité adverse.", UsageCost = 1, IsAttack = true, TargetIds = demoTargets, TargetTypes = new[] { GameTargetType.EnemyCharacterOrLeader } } } };
        GamePlayerDto Player(bool self) => new() { UserId = self ? "me" : "them", DisplayName = self ? "Joueur test" : "Adversaire test", TurnsPlayed = 2, Money = 5, RemainingSeconds = 1200, DeckCount = 35, Leader = Card(1), Characters = new GameCardDto?[] { ChangeCard(Card(2), blocking: true), null, null, null }, Hand = self ? Enumerable.Range(0, 16).Select(i => Card(i % 2 + 2)).ToArray() : Array.Empty<GameCardDto>() };
        var opponent = Player(false);
        demoTargets.Add(opponent.Characters[0]!.InstanceId);
        States[Context.ConnectionId] = new GameStateDto { MatchId = matchId, IsMyTurn = true, Status = GameMatchStatus.InProgress, ClockAnchorUtc = DateTime.UtcNow, ServerTimeUtc = DateTime.UtcNow, TurnNumber = 1, Me = Player(true), Opponent = opponent, ActivePlayerId = "me" };
        await Clients.Caller.SendAsync("GameStateUpdated", States[Context.ConnectionId]);
        for (var i = 1; i <= 120; i++)
        {
            await Clients.Caller.SendAsync("GameLogAdded", new { message = $"Événement de démonstration {i} : vérification du défilement indépendant.", createdAt = DateTime.UtcNow });
            await Clients.Caller.SendAsync("ChatMessageReceived", new { username = "Adversaire test", message = $"Message de démonstration {i}.", createdAt = DateTime.UtcNow });
        }
    }
    public Task PlayCard(Guid matchId, Guid cardInstanceId, int? slot)
    {
        var state = States[Context.ConnectionId]; var me = state.Me;
        var card = me.Hand.First(c => c.InstanceId == cardInstanceId);
        if (!slot.HasValue || slot < 0 || slot > 3 || me.Characters[slot.Value] != null) throw new HubException("Emplacement invalide ou occupé.");
        var characters = me.Characters.ToArray(); characters[slot.Value] = card;
        var updated = CopyPlayer(me, money: me.Money - card.UsageCost, characters: characters, hand: me.Hand.Where(c => c.InstanceId != cardInstanceId).ToArray());
        States[Context.ConnectionId] = new GameStateDto { MatchId = matchId, IsMyTurn = true, Status = GameMatchStatus.InProgress, ClockAnchorUtc = DateTime.UtcNow, ServerTimeUtc = DateTime.UtcNow, TurnNumber = 1, Me = updated, Opponent = state.Opponent, ActivePlayerId = "me" };
        return Clients.Caller.SendAsync("GameStateUpdated", States[Context.ConnectionId]);
    }
    private static GamePlayerDto CopyPlayer(GamePlayerDto p, int? money = null, GameCardDto? leader = null, IReadOnlyList<GameCardDto?>? characters = null, IReadOnlyList<GameCardDto>? hand = null) => new()
    { UserId = p.UserId, DisplayName = p.DisplayName, Money = money ?? p.Money, TurnsPlayed = p.TurnsPlayed, RemainingSeconds = p.RemainingSeconds, DeckCount = p.DeckCount, Leader = leader ?? p.Leader, Characters = characters ?? p.Characters, Hand = hand ?? p.Hand };
    private static GameCardDto ChangeCard(GameCardDto c, int damage = 0, bool blocking = false) => new()
    { InstanceId = c.InstanceId, CardId = c.CardId, Name = c.Name, Type = c.Type, MaxHealth = c.MaxHealth, CurrentHealth = c.CurrentHealth - damage, UsageCost = c.UsageCost, Abilities = c.Abilities, IsExhausted = blocking || c.IsExhausted, IsBlocking = blocking || c.IsBlocking };
    public Task UseAbility(Guid matchId, Guid source, int abilityId, Guid? target)
    {
        var state = States[Context.ConnectionId];
        if (state.Me.Money < 1) throw new HubException("Énergie insuffisante.");
        var opponent = CopyPlayer(state.Opponent,
            leader: state.Opponent.Leader.InstanceId == target ? ChangeCard(state.Opponent.Leader, 20) : null,
            characters: state.Opponent.Characters.Select(c => c != null && c.InstanceId == target ? ChangeCard(c, 20) : c).ToArray());
        States[Context.ConnectionId] = new() { MatchId = matchId, IsMyTurn = true, Status = GameMatchStatus.InProgress, ClockAnchorUtc = DateTime.UtcNow, ServerTimeUtc = DateTime.UtcNow, Me = CopyPlayer(state.Me, money: state.Me.Money - 1), Opponent = opponent };
        return Clients.Caller.SendAsync("GameStateUpdated", States[Context.ConnectionId]);
    }
    public Task Block(Guid matchId, Guid source)
    {
        var state = States[Context.ConnectionId];
        var me = CopyPlayer(state.Me, leader: state.Me.Leader.InstanceId == source ? ChangeCard(state.Me.Leader, blocking: true) : null,
            characters: state.Me.Characters.Select(c => c?.InstanceId == source ? ChangeCard(c, blocking: true) : c).ToArray());
        States[Context.ConnectionId] = new() { MatchId = matchId, IsMyTurn = true, Status = GameMatchStatus.InProgress, ClockAnchorUtc = DateTime.UtcNow, ServerTimeUtc = DateTime.UtcNow, Me = me, Opponent = state.Opponent };
        return Clients.Caller.SendAsync("GameStateUpdated", States[Context.ConnectionId]);
    }
    // Fins de partie fictives pour contrôler les deux écrans sans base de données.
    public Task EndTurn(Guid matchId) => FinishPreview(matchId, true);
    public Task Surrender(Guid matchId) => FinishPreview(matchId, false);
    private Task FinishPreview(Guid matchId, bool won)
    {
        var state = States[Context.ConnectionId];
        States[Context.ConnectionId] = new GameStateDto
        {
            MatchId = matchId, Status = GameMatchStatus.Finished, Me = state.Me, Opponent = state.Opponent,
            TurnNumber = state.TurnNumber, WinnerUserId = won ? state.Me.UserId : state.Opponent.UserId,
            EndReason = won ? GameEndReason.LeaderDefeated : GameEndReason.Surrender,
            XpEarned = won ? 100 : 0, CardBalanceEarned = won ? 10.5m : 0m,
            ClockAnchorUtc = DateTime.UtcNow, ServerTimeUtc = DateTime.UtcNow
        };
        return Clients.Caller.SendAsync("GameStateUpdated", States[Context.ConnectionId]);
    }
    public override Task OnDisconnectedAsync(Exception? exception) { States.TryRemove(Context.ConnectionId, out _); return base.OnDisconnectedAsync(exception); }
}
