using web_tcg.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using web_tcg.Data;
using web_tcg.Data.Entities;

if (args.Contains("--parties")) { await PoliticalPartyChecks.RunAsync(); return; }
if (args.Contains("--friends")) { await FriendsChecks.RunAsync(); return; }

if (args.Contains("--market"))
{
    await MarketChecks.RunAsync();
    return;
}

if (args.Contains("--secret"))
{
    await SecretChecks.RunAsync();
    return;
}

if (args.Contains("--cardsets"))
{
    await CardSetChecks.RunAsync();
    return;
}

if (args.Contains("--preview"))
{
    await Preview.RunAsync();
    return;
}

var queue = new MatchmakingService();
var firstDeck = Guid.NewGuid();
var secondDeck = Guid.NewGuid();
var passedChecks = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception(name);
    passedChecks++;
    Console.WriteLine($"PASS: {name}");
}
Check(await queue.TryEnqueueAndMatchAsync("alice", firstDeck, connectionId: "old") == null, "premier joueur en attente");
Check(await queue.TryEnqueueAndMatchAsync("alice", secondDeck, connectionId: "new") == null, "pas d'auto-appariement");
queue.Leave("alice", "old");
Check(queue.IsQueued("alice"), "une ancienne connexion ne supprime pas la nouvelle recherche");
var opponent = await queue.TryEnqueueAndMatchAsync("bob", firstDeck, connectionId: "bob");
Check(opponent?.UserId == "alice" && opponent.DeckId == secondDeck && queue.QueueCount == 0, "appariement avec le dernier deck choisi");
await queue.TryEnqueueAndMatchAsync("alice", firstDeck, connectionId: "new");
queue.Leave("alice", "new");
Check(queue.QueueCount == 0, "annulation effective");

var players = Enumerable.Range(0, 400).Select(i => $"player-{i}").ToArray();
var matched = new System.Collections.Concurrent.ConcurrentBag<string>();
await Parallel.ForEachAsync(players, async (player, ct) =>
{
    var entry = await queue.TryEnqueueAndMatchAsync(player, firstDeck, ct);
    if (entry != null) { matched.Add(player); matched.Add(entry.UserId); }
});
Check(queue.QueueCount == 0 && matched.Count == 400 && matched.Distinct().Count() == 400, "400 arrivées concurrentes sans perte ni doublon");

using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql("Host=localhost;Database=tracking_test;Username=unused;Password=unused").Options);
var existingDeck = new Deck { UserId = "bot", Name = "Deck IA", LeaderCardId = 1 };
context.Attach(existingDeck);
var newCard = new DeckCard { CardId = 2, Quantity = 4 };
existingDeck.Cards.Add(newCard);
context.ChangeTracker.DetectChanges();
Check(context.Entry(newCard).State == EntityState.Modified, "reproduction du mauvais suivi d'une nouvelle carte avec GUID prédéfini");
context.ChangeTracker.Clear();

existingDeck.Cards.Clear();
var unchangedCard = new DeckCard { DeckId = existingDeck.Id, CardId = 3, Quantity = 2 };
existingDeck.Cards.Add(unchangedCard);
context.Attach(existingDeck);
var addedCard = DeckCardPersistence.Add(context, existingDeck, 2, 4);
context.ChangeTracker.DetectChanges();
Check(context.Entry(addedCard).State == EntityState.Added, "carte ajoutée à un deck existant suivie comme insertion");
Check(context.Entry(existingDeck).State == EntityState.Unchanged && context.Entry(unchangedCard).State == EntityState.Unchanged,
    "l'ajout ne réinsère pas le deck ni les cartes existantes");
Check(existingDeck.Cards.Count == 2 && existingDeck.Cards.Contains(addedCard) && addedCard.DeckId == existingDeck.Id,
    "navigation et clé étrangère du deck restent cohérentes");
unchangedCard.Quantity = 3;
context.ChangeTracker.DetectChanges();
Check(context.Entry(unchangedCard).State == EntityState.Modified, "une carte existante reste modifiable");
var createdDeck = new Deck { UserId = "player", Name = "Nouveau deck", LeaderCardId = 1 };
context.Add(createdDeck);
var createdCard = DeckCardPersistence.Add(context, createdDeck, 4, 1);
context.ChangeTracker.DetectChanges();
Check(context.Entry(createdDeck).State == EntityState.Added && context.Entry(createdCard).State == EntityState.Added,
    "création d'un deck et de ses cartes suivie comme insertion");
var arrayIds = new[] { 1, 2 };
var spanFailureReproduced = false;
try
{
    _ = context.Cards.Where(card => arrayIds.Contains(card.Id)).ToQueryString();
}
catch (InvalidOperationException exception) when (exception.GetBaseException() is TypeLoadException)
{
    spanFailureReproduced = true;
}
Check(spanFailureReproduced, "reproduction de l'échec ReadOnlySpan avec un tableau dans une requête EF");

var presentationIds = arrayIds.Distinct().ToList();
var cardsSql = context.Cards.AsNoTracking().Where(card => presentationIds.Contains(card.Id))
    .Include(card => card.Region).Include(card => card.Country).Include(card => card.CardSet)
    .Include(card => card.Abilities).Include(card => card.BaseCard).ThenInclude(card => card!.Abilities)
    .ToQueryString();
Check(cardsSql.Contains("SELECT") && cardsSql.Contains("ANY"), "traduction PostgreSQL des cartes et variantes sans ReadOnlySpan");
var gameplayIds = new List<int> { 1, 2 };
Check(context.LeaderCards.AsNoTracking().Where(card => gameplayIds.Contains(card.CardId)).ToQueryString().Contains("ANY"),
    "traduction PostgreSQL des points de vie des Leaders");
Check(context.CharacterCards.AsNoTracking().Where(card => gameplayIds.Contains(card.CardId)).ToQueryString().Contains("ANY"),
    "traduction PostgreSQL des points de vie des personnages");
Check((await new CardPresentationService(context).GetAsync(Array.Empty<int>())).Count == 0,
    "collection vide traitée sans requête SQL");
var engine = new web_tcg.Game.Engine.GameEngine(
    new web_tcg.Game.Effects.GameEffectEngine(Array.Empty<web_tcg.Game.Effects.IGameEffectHandler>(), new web_tcg.Game.Engine.GameTargetResolver()),
    new web_tcg.Game.Engine.GameConditionEvaluator());
var gameState = new web_tcg.Game.Runtime.GameState
{
    Status = web_tcg.Data.Enums.GameMatchStatus.InProgress, ActivePlayerId = "alice",
    PlayerOne = new() { UserId = "alice", Money = 50, Leader = new() { CurrentHealth = 1000 } },
    PlayerTwo = new() { UserId = "bob", Leader = new() { CurrentHealth = 1000 } }
};
var handCharacter = new web_tcg.Game.Runtime.GameCardState { Type = web_tcg.Data.Enums.CardType.Character, UsageCost = 20, CurrentHealth = 100 };
gameState.PlayerOne.Hand.Add(handCharacter);
async Task RejectPose(int? slot, string message)
{
    try { await engine.PlayCardAsync(gameState, "alice", handCharacter.InstanceId, slot); throw new Exception("Pose acceptée à tort"); }
    catch (web_tcg.Game.Engine.GameRuleException) { }
    Check(gameState.PlayerOne.Money == 50 && gameState.PlayerOne.Hand.Contains(handCharacter), message);
}
await RejectPose(null, "emplacement manquant : carte et énergie conservées");
await RejectPose(4, "emplacement hors plateau : carte et énergie conservées");
gameState.PlayerOne.Characters[0] = new() { CurrentHealth = 100 };
await RejectPose(0, "emplacement occupé : carte et énergie conservées");
handCharacter.Type = web_tcg.Data.Enums.CardType.Location;
gameState.PlayerOne.Location = new();
await RejectPose(null, "lieu déjà posé : carte et énergie conservées");
handCharacter.Type = web_tcg.Data.Enums.CardType.Character;
await engine.PlayCardAsync(gameState, "alice", handCharacter.InstanceId, 2);
Check(gameState.PlayerOne.Characters[2] == handCharacter && !gameState.PlayerOne.Hand.Contains(handCharacter) && gameState.PlayerOne.Money == 30,
    "pose d'un personnage : emplacement, main et énergie mis à jour");
handCharacter.Abilities.Add(new() { Id = 7, Type = web_tcg.Data.Enums.AbilityType.Skill, Effects = new()
{
    new() { Trigger = web_tcg.Data.Enums.GameEffectTrigger.OnSkill, Target = web_tcg.Data.Enums.GameTargetType.EnemyCharacterOrLeader },
    new() { Trigger = web_tcg.Data.Enums.GameEffectTrigger.OnDeath, Target = web_tcg.Data.Enums.GameTargetType.FriendlyCharacter }
} });
gameState.PlayerOne.TurnsPlayed = 2;
gameState.PlayerTwo.Hand.Add(new() { CardId = 99 });
var projected = new GameStateProjector().Project(gameState, "alice");
Check(projected.Me.TurnsPlayed == 2 && projected.Me.Characters[2]!.Abilities[0].TargetTypes.SequenceEqual(new[] { web_tcg.Data.Enums.GameTargetType.EnemyCharacterOrLeader }),
    "popup : phase et seules cibles de la compétence active transmises");
Check(projected.Opponent.Hand.Count == 0, "popup : aucune carte de la main adverse divulguée");
gameState.ClockAnchorUtc = DateTime.UtcNow.AddSeconds(-25);
Check(new GameStateProjector().Project(gameState, "alice").ClockAnchorUtc == gameState.ClockAnchorUtc,
    "chrono : référence serveur transmise même après 25 secondes sans action");
// Les récompenses restent personnelles et conservent leurs montants après sérialisation.
gameState.Status = web_tcg.Data.Enums.GameMatchStatus.Finished;
gameState.WinnerUserId = "alice";
gameState.RewardXp = 123;
gameState.RewardCardBalance = 10.5m;
var restoredResult = System.Text.Json.JsonSerializer.Deserialize<web_tcg.Game.Runtime.GameState>(System.Text.Json.JsonSerializer.Serialize(gameState))!;
var winnerResult = new GameStateProjector().Project(restoredResult, "alice");
var loserResult = new GameStateProjector().Project(restoredResult, "bob");
Check(winnerResult.XpEarned == 123 && winnerResult.CardBalanceEarned == 10.5m, "fin : récompenses effectivement créditées conservées après rechargement");
Check(loserResult.XpEarned == 0 && loserResult.CardBalanceEarned == 0, "fin : aucune récompense du vainqueur affichée au perdant");
restoredResult.RewardXp = null; restoredResult.RewardCardBalance = null;
Check(new GameStateProjector().Project(restoredResult, "alice").XpEarned == null, "fin : montants historiques inconnus jamais inventés");

var aiState = new web_tcg.Game.Runtime.GameState
{
    Status = web_tcg.Data.Enums.GameMatchStatus.InProgress, ActivePlayerId = "bot", FirstPlayerId = "bot", TurnNumber = 2,
    ClockAnchorUtc = DateTime.UtcNow, TurnStartedAtUtc = DateTime.UtcNow,
    PlayerOne = new() { UserId = "bot", Money = 100, TurnsPlayed = 2, Leader = new() { Type = web_tcg.Data.Enums.CardType.Leader, CurrentHealth = 1000, MaxHealth = 1000 } },
    PlayerTwo = new() { UserId = "human", Leader = new() { Type = web_tcg.Data.Enums.CardType.Leader, CurrentHealth = 1000, MaxHealth = 1000 } }
};
for (var i = 0; i < 2; i++) aiState.PlayerOne.Hand.Add(new() { Name = "Carte IA", Type = web_tcg.Data.Enums.CardType.Character, UsageCost = 20, CurrentHealth = 100, MaxHealth = 100 });
var aiFrames = new List<(int Cards, string Active)>();
var streamedLogs = new List<string>();
var aiLogs = await new GameAiService(engine).PlayTurnAsync(aiState, "bot", onAction: messages =>
{
    aiFrames.Add((aiState.PlayerOne.Characters.Count(c => c != null), aiState.ActivePlayerId));
    streamedLogs.AddRange(messages);
    return Task.CompletedTask;
});
Check(aiFrames.Count == 3 && aiFrames[0] == (1, "bot") && aiFrames[1] == (2, "bot") && aiFrames[2].Active == "human", "IA : chaque pose et fin de tour transmise dans l’ordre");
Check(streamedLogs.SequenceEqual(aiLogs), "IA : historique progressif complet sans doublon");

await AbilityRuleChecks.RunAsync(Check, context);
await GeographyChecks.RunAsync(Check);
await PartyRuleChecks.RunAsync(Check);
await EffectAuditChecks.RunAsync(Check);
await ActionFlowAuditChecks.RunAsync(Check);
await OrientationRuleChecks.RunAsync(Check);
var auctionStart = new DateTime(2026,10,7,12,0,0,DateTimeKind.Utc);
Check(MarketService.AuctionEnd(auctionStart,10) == auctionStart.AddMinutes(10)
    && MarketService.AuctionEnd(auctionStart,30) == auctionStart.AddMinutes(30)
    && MarketService.AuctionEnd(auctionStart,720) == auctionStart.AddHours(12),
    "marché : durées de 10 minutes, 30 minutes et 12 heures converties sans erreur d'unité");
try { MarketService.AuctionEnd(auctionStart,5); Check(false,"marché : durée non autorisée refusée"); }
catch(MarketRuleException) { Check(true,"marché : durée non autorisée refusée"); }
await HandCostChecks.RunAsync(Check);
await CardImageChecks.RunAsync(Check);
CardPreviewChecks.Run(Check);
BoosterDropChecks.Run(Check);
var releaseNow = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
var releaseSets = new[]
{
    new CardSet { Id = 1, IsActive = true, ReleaseDate = releaseNow.AddDays(-1) },
    new CardSet { Id = 2, IsActive = false, ReleaseDate = releaseNow.AddDays(-1) },
    new CardSet { Id = 3, IsActive = true, ReleaseDate = releaseNow.AddDays(1) },
    new CardSet { Id = 4, IsActive = false, ReleaseDate = releaseNow.AddDays(1) },
    new CardSet { Id = 5, IsActive = true, ReleaseDate = releaseNow }
};
Check(releaseSets.AsQueryable().AvailableForBoosters(releaseNow).Select(x => x.Id).SequenceEqual(new[] { 1, 5 }),
    "boosters : seules les extensions actives déjà publiées sont accessibles, date exacte incluse");
Check(context.CardSets.AvailableForBoosters(releaseNow).ToQueryString().Contains("ReleaseDate"),
    "boosters : filtre de disponibilité traduit en SQL PostgreSQL");
TargetingChecks.Run(Check);
await PasswordRecoveryChecks.RunAsync(Check);
var geographyMigration = context.Database.GetMigrations().Last();
var migrationSql = context.GetService<IMigrator>().GenerateScript("20261001113617_AddGameAndBotSupport",geographyMigration);
Check(migrationSql.Contains("ParametersJson") && migrationSql.Contains("DEFAULT '{}'"), "migration : nouveau JSON des conditions initialisé avec un objet valide pour les anciennes lignes");
Console.WriteLine($"{passedChecks} vérifications backend réussies.");
