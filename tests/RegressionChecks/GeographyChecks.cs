using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Effects;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;
using web_tcg.Models.Admin;
using web_tcg.Services;

public static class GeographyChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        GameCardState Unit(string owner,int region,int? country,CardType type = CardType.Character) => new() {
            OwnerUserId = owner, ControllerUserId = owner, RegionId = region, CountryId = country, Type = type,
            Zone = type == CardType.Leader ? GameCardZone.Leader : GameCardZone.CharacterBoard, BaseHealth = 100, MaxHealth = 100, CurrentHealth = 100 };
        var state = new GameState { Status = GameMatchStatus.InProgress, ActivePlayerId = "a",
            PlayerOne = new() { UserId = "a", TurnsPlayed = 2, Leader = Unit("a",1,10,CardType.Leader) },
            PlayerTwo = new() { UserId = "b", TurnsPlayed = 2, Leader = Unit("b",1,10,CardType.Leader) } };
        var first = Unit("a",1,10); var second = Unit("a",2,20); var third = Unit("a",3,null); var enemy = Unit("b",1,10);
        state.PlayerOne.Characters[0] = first; state.PlayerOne.Characters[1] = second; state.PlayerOne.Characters[2] = third; state.PlayerTwo.Characters[0] = enemy;
        var effectEngine = new GameEffectEngine(new IGameEffectHandler[] { new HealthBonusEffectHandler(),new DamageBonusEffectHandler(),new DamageEffectHandler(),new DestroyLocationEffectHandler() },new GameTargetResolver());
        var engine = new GameEngine(effectEngine,new GameConditionEvaluator());
        var input = new CardAbilityInputModel { Type = AbilityType.Effect, Effects = new() {
            new() { Type = GameEffectType.HealthBonus, Trigger = GameEffectTrigger.Passive, Target = GameTargetType.AllCharacters, Value = 20, RegionIds = new() {1,2}, CountryIds = new() {10,20} },
            new() { Type = GameEffectType.DamageBonus, Trigger = GameEffectTrigger.Passive, Target = GameTargetType.Self, Value = 20,
                ScaleByMatchingCharacters = true, CountScope = 1, CountRegionIds = new() {1,2} } },
            Conditions = new() { new() { Type = GameConditionType.TargetRegion, RegionIds = new() {1,2} }, new() { Type = GameConditionType.TargetCountry, CountryIds = new() {10,20} } } };
        var stored = new CardAbility(); CardAbilityEditor.Write(stored,input); var loaded = CardAbilityEditor.Read(stored);
        check(loaded.Effects[0].RegionIds.SequenceEqual(new[] {1,2}) && loaded.Effects[0].CountryIds.SequenceEqual(new[] {10,20}) && loaded.Effects[1].CountRegionIds.SequenceEqual(new[] {1,2}) && loaded.Effects[1].ScaleByMatchingCharacters && loaded.Conditions[1].CountryIds.SequenceEqual(new[] {10,20}),
            "géographie : sélections multiples et calcul par personnage conservés à l'enregistrement");
        check(!CardAbilityEditor.Validate(CardType.Character,input).Any(), "géographie : bonus de PV et attaque géographiques validés");
        GameAbilityState Ability(CardAbility entity) => new() { Type = entity.Type, Effects = entity.Effects.Select(e => new GameEffectDefinition { Type = e.Type, Trigger = e.Trigger, Target = e.Target, Value = e.Value, Parameters = GameGeography.Parse(e.ParametersJson) }).ToList(),
            Conditions = entity.Conditions.Select(c => new GameConditionDefinition { Type = c.Type, Value = c.Value, Negate = c.Negate, Parameters = GameGeography.Parse(c.ParametersJson) }).ToList() };
        var health = Ability(stored); health.Effects.RemoveAt(1);
        var ctx = new GameEffectContext { State = state, Player = state.PlayerOne, Source = first, TargetConditions = health.Conditions };
        await effectEngine.ExecuteAsync(ctx,health.Effects,GameEffectTrigger.Passive);
        check(first.MaxHealth == 120 && second.MaxHealth == 120 && enemy.MaxHealth == 120 && third.MaxHealth == 100 && state.PlayerOne.Leader.MaxHealth == 100,
            "géographie : +20 PV uniquement aux personnages correspondants sur les deux terrains, Leaders exclus");
        await effectEngine.ExecuteAsync(ctx,health.Effects,GameEffectTrigger.Passive);
        check(first.MaxHealth == 120 && first.CurrentHealth == 120, "géographie : une aura de PV ne cumule pas son bonus à chaque recalcul");
        var conditionEvaluator = new GameConditionEvaluator();
        check(conditionEvaluator.AreMet(state,state.PlayerOne,first,first,health.Conditions) && !conditionEvaluator.AreMet(state,state.PlayerOne,first,third,health.Conditions),
            "géographie : conditions de région et pays filtrent chaque bénéficiaire");
        var countCondition = new GameConditionDefinition { Type = GameConditionType.MatchingCharactersAtLeast, Value = 3, Parameters = new() { ["regions"]="1,2" } };
        check(conditionEvaluator.AreMet(state,state.PlayerOne,first,null,new[] {countCondition}) && GameGeography.Count(state,state.PlayerOne,countCondition.Parameters,scope:1) == 2,
            "géographie : comptage sur un ou deux terrains sans inclure les Leaders");
        var attack = Ability(stored); attack.Conditions.Clear(); attack.Effects.RemoveAt(0);
        first.Abilities.Add(health); first.Abilities.Add(attack);
        var noOp = Unit("a",9,99,CardType.PoliticalDecision); noOp.Zone = GameCardZone.Hand; state.PlayerOne.Hand.Add(noOp);
        await engine.PlayCardAsync(state,"a",noOp.InstanceId,null);
        check(first.DamageBonus == 40, "géographie : +20 dégâts par personnage correspondant donne +40 pour deux personnages alliés");
        first.Abilities.Add(new() { Id = 8, Type = AbilityType.Skill, Effects = new() { new() { Type = GameEffectType.Damage, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.EnemyLeader, Value = 10 } } });
        await engine.UseAbilityAsync(state,"a",first.InstanceId,8,state.PlayerTwo.Leader.InstanceId);
        check(state.PlayerTwo.Leader.CurrentHealth == 50, "géographie : compétence de 10 dégâts inflige 50 avec le bonus fixe de 40");
        second.CurrentHealth = 0;
        check(new GameTargetResolver().ResolveCards(state,state.PlayerOne,first,new() { Target = GameTargetType.AllFriendlyCharacters },null).All(c => c.InstanceId != second.InstanceId),
            "géographie : un bonus collectif ne cible pas un personnage mort avant son déplacement au cimetière");
        var next = Unit("a",9,99,CardType.PoliticalDecision); next.Zone = GameCardZone.Hand; state.PlayerOne.Hand.Add(next);
        await engine.PlayCardAsync(state,"a",next.InstanceId,null);
        check(first.DamageBonus == 20 && state.PlayerOne.Characters[1] == null, "géographie : disparition d'un personnage réduit automatiquement le bonus proportionnel");
        state.PlayerOne.Characters[0] = null; state.PlayerOne.Graveyard.Add(first);
        var last = Unit("a",9,99,CardType.PoliticalDecision); last.Zone = GameCardZone.Hand; state.PlayerOne.Hand.Add(last);
        await engine.PlayCardAsync(state,"a",last.InstanceId,null);
        check(enemy.MaxHealth == 100 && enemy.CurrentHealth == 100, "géographie : disparition de la source retire le bonus régional de PV");
        var filter = new GameEffectDefinition { Target = GameTargetType.AllCharacters, Parameters = new() { ["regions"]="1", ["countries"]="20" } };
        check(new GameTargetResolver().ResolveCards(state,state.PlayerOne,third,filter,null).Count == 0, "géographie : région et pays sont combinés avec ET, pays absent ne correspond pas");
        await effectEngine.ExecuteAsync(new GameEffectContext { State = state, Player = state.PlayerOne, Source = third },
            new[] { new GameEffectDefinition { Type = GameEffectType.HealthBonus, Trigger = GameEffectTrigger.OnPlay, Target = GameTargetType.Self, Value = 20, DurationTurns = 1 } },GameEffectTrigger.OnPlay);
        await engine.EndTurnAsync(state,"a"); await engine.EndTurnAsync(state,"b");
        check(third.MaxHealth == 120, "géographie : bonus de PV temporaire conservé pendant le prochain tour du propriétaire");
        await engine.EndTurnAsync(state,"a"); await engine.EndTurnAsync(state,"b");
        check(third.MaxHealth == 100 && third.CurrentHealth == 100, "géographie : expiration du bonus de PV restaure les statistiques de base");
    }
}
