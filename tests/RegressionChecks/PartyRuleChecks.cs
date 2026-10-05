using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Effects;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;
using web_tcg.Models.Admin;
using web_tcg.Services;

public static class PartyRuleChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        GameCardState Unit(string owner,int? party,CardType type = CardType.Character) => new() { OwnerUserId = owner, ControllerUserId = owner, PoliticalPartyId = party,
            Type = type, Zone = type == CardType.Leader ? GameCardZone.Leader : GameCardZone.CharacterBoard, CurrentHealth = 100, MaxHealth = 100, BaseHealth = 100 };
        var first = Unit("a",1); var other = Unit("a",2); var neutral = Unit("a",null); var enemy = Unit("b",1);
        var state = new GameState { Status = GameMatchStatus.InProgress, ActivePlayerId = "a",
            PlayerOne = new() { UserId = "a", TurnsPlayed = 2, Leader = Unit("a",1,CardType.Leader), Characters = new() { first,other,neutral,null } },
            PlayerTwo = new() { UserId = "b", TurnsPlayed = 2, Leader = Unit("b",2,CardType.Leader), Characters = new() { enemy,null,null,null } } };
        var input = new CardAbilityInputModel { Type = AbilityType.Skill, Effects = new() { new() { Type = GameEffectType.DamageBonus, Trigger = GameEffectTrigger.OnSkill,
            Target = GameTargetType.AllCharacters, Value = -20, DurationTurns = 1, PartyIds = new() {1,2}, CountPartyIds = new() {1}, ScaleByMatchingCharacters = true } },
            Conditions = new() { new() { Type = GameConditionType.TargetParty, PartyIds = new() {1,2} } } };
        var stored = new CardAbility(); CardAbilityEditor.Write(stored,input); var read = CardAbilityEditor.Read(stored);
        check(read.Effects[0].PartyIds.SequenceEqual(new[] {1,2}) && read.Effects[0].CountPartyIds.SequenceEqual(new[] {1}) && read.Conditions[0].PartyIds.SequenceEqual(new[] {1,2}),
            "partis : filtres, conditions et comptage conservés en modification");
        check(!CardAbilityEditor.Validate(CardType.Character,input).Any(), "partis : malus d'attaque par personnage valide");
        var evaluator = new GameConditionEvaluator();
        var condition = new GameConditionDefinition { Type = GameConditionType.TargetParty, Parameters = new() { ["parties"] = "1" } };
        check(evaluator.AreMet(state,state.PlayerOne,first,enemy,new[] {condition}) && !evaluator.AreMet(state,state.PlayerOne,first,other,new[] {condition}) &&
            evaluator.Failure(state,state.PlayerOne,first,other,new[] {condition})!.Contains("parti"), "partis : condition de cible et erreur explicite");
        condition.Type = GameConditionType.SourceParty;
        check(evaluator.AreMet(state,state.PlayerOne,first,null,new[] {condition}) && !evaluator.AreMet(state,state.PlayerOne,neutral,null,new[] {condition}), "partis : condition de source, carte sans parti exclue");
        var filter = new GameEffectDefinition { Type = GameEffectType.HealthBonus, Trigger = GameEffectTrigger.OnPlay, Target = GameTargetType.AllCharacters, Value = 20, Parameters = new() { ["parties"] = "1" } };
        var resolver = new GameTargetResolver();
        check(resolver.ResolveCards(state,state.PlayerOne,first,filter,null).Select(c=>c.InstanceId).ToHashSet().SetEquals(new[] { first.InstanceId,enemy.InstanceId }),
            "partis : groupe des deux terrains filtré sans Leader ni carte sans parti");
        var effects = new GameEffectEngine(new IGameEffectHandler[] {new HealthBonusEffectHandler(),new DamageBonusEffectHandler(),new DamageEffectHandler()},resolver);
        var context = new GameEffectContext { State = state, Player = state.PlayerOne, Source = first };
        await effects.ExecuteAsync(context,new[] {filter},GameEffectTrigger.OnPlay);
        check(first.MaxHealth == 120 && enemy.MaxHealth == 120 && other.MaxHealth == 100 && neutral.MaxHealth == 100,"partis : bonus de PV limité aux bénéficiaires du parti");
        filter.Type = GameEffectType.DamageBonus; filter.Value = -20; filter.Parameters["scaleByCount"] = "true"; filter.Parameters["countparties"] = "1"; filter.Parameters["countScope"] = "3";
        await effects.ExecuteAsync(context,new[] {filter},GameEffectTrigger.OnPlay);
        check(first.DamageBonus == -40 && enemy.DamageBonus == -40 && other.DamageBonus == 0,"partis : malus multiplié par les deux personnages correspondants");
        var before = enemy.CurrentHealth;
        await effects.ExecuteAsync(new GameEffectContext { State = state, Player = state.PlayerOne, Source = first, RequestedTargetId = enemy.InstanceId },
            new[] {new GameEffectDefinition { Type = GameEffectType.Damage, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.EnemyCharacter, Value = 10 }},GameEffectTrigger.OnSkill);
        check(enemy.CurrentHealth == before,"malus : dégâts réduits à zéro sans soigner la cible");
        filter.Type = GameEffectType.HealthBonus; filter.Value = -1000; filter.Parameters.Remove("scaleByCount");
        await effects.ExecuteAsync(context,new[] {filter},GameEffectTrigger.OnPlay);
        check(first.MaxHealth == 0 && first.CurrentHealth == 0,"malus : PV bornés à zéro sans exception");
    }
}
