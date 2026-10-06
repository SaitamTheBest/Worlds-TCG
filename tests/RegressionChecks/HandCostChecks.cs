using System.Text.Json;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Effects;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;
using web_tcg.Models.Admin;
using web_tcg.Models.Game;
using web_tcg.Services;

public static class HandCostChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        GameCardState Card(string owner, int region=1) => new() { OwnerUserId=owner, ControllerUserId=owner, Name="Carte test", Type=CardType.Character,
            RegionId=region, CountryId=1, PoliticalPartyId=1, Zone=GameCardZone.Hand, BaseHealth=100,MaxHealth=100,CurrentHealth=100 };
        var input=new CardAbilityInputModel { Type=AbilityType.Trait, Name="Négociation", Description="Révéler et défausser", UsageCost=4,
            Effects=new() { new() { Type=GameEffectType.GainMoney,Trigger=GameEffectTrigger.OnTalent,Target=GameTargetType.FriendlyPlayer,Value=10 } },
            Conditions=new() { new() { Type=GameConditionType.RevealHandCards,Value=2,RegionIds=new(){1},CountryIds=new(){1},PartyIds=new(){1},CardTypes=new(){2} },
                new() { Type=GameConditionType.DiscardHandCards,Value=1 } } };
        var stored=new CardAbility(); CardAbilityEditor.Write(stored,input); var read=CardAbilityEditor.Read(stored);
        check(read.Conditions[0].CardTypes.SequenceEqual(new[]{2}) && read.Conditions[0].PartyIds.SequenceEqual(new[]{1}) && !CardAbilityEditor.Validate(CardType.Leader,input).Any(),
            "coûts de main : nombre et filtres enregistrés puis relus");
        var source=Card("a"); source.Type=CardType.Leader;source.Zone=GameCardZone.Leader;
        source.Abilities.Add(new() { Id=1,Type=AbilityType.Trait,UsageCost=4,Effects=new() { new() { Type=GameEffectType.GainMoney,Trigger=GameEffectTrigger.OnTalent,Target=GameTargetType.FriendlyPlayer,Value=10 } },
            Conditions=stored.Conditions.Select(c=>new GameConditionDefinition { Type=c.Type,Value=c.Value,Parameters=GameGeography.Parse(c.ParametersJson) }).ToList() });
        var first=Card("a");var second=Card("a");var discarded=Card("a",2);var enemy=Card("b");
        var state=new GameState { Status=GameMatchStatus.InProgress,ActivePlayerId="a",TurnNumber=5,
            PlayerOne=new(){UserId="a",Leader=source,TurnsPlayed=3,Money=100,Hand=new(){first,second,discarded}},
            PlayerTwo=new(){UserId="b",Leader=Card("b"),TurnsPlayed=3,Hand=new(){enemy}} };
        var projector=new GameStateProjector();var own=projector.Project(state,"a").Me.Leader.Abilities[0].HandCosts;
        check(own.Count==2 && own[0].EligibleIds.ToHashSet().SetEquals(new[]{first.InstanceId,second.InstanceId}),
            "coûts de main : seules les cartes du bon type et des bonnes origines proposées");
        var other=projector.Project(state,"b");
        check(other.Opponent.Hand.Count==0 && other.Opponent.Leader.Abilities[0].HandCosts.Count==0,
            "coûts de main : liste des cartes autorisées jamais envoyée à l'adversaire");
        var effects=new GameEffectEngine(new IGameEffectHandler[]{new GainMoneyEffectHandler()},new GameTargetResolver());var engine=new GameEngine(effects,new GameConditionEvaluator());
        List<GameHandCostSelection> Choices(Guid reveal2,Guid discard)=>new() { new(){ConditionIndex=0,CardIds=new(){first.InstanceId,reveal2}},new(){ConditionIndex=1,CardIds=new(){discard}} };
        async Task<bool> Reject(List<GameHandCostSelection>? choices) { try { await engine.UseAbilityAsync(state,"a",source.InstanceId,1,null,handCosts:choices);return false; } catch(GameRuleException) { return true; } }
        check(await Reject(null) && await Reject(Choices(enemy.InstanceId,discarded.InstanceId)) && await Reject(Choices(first.InstanceId,discarded.InstanceId))
            && await Reject(Choices(second.InstanceId,first.InstanceId)),"coûts de main : absence, carte adverse, doublon et double paiement refusés");
        check(state.PlayerOne.Money==100 && state.PlayerOne.Hand.Count==3 && state.PlayerOne.Graveyard.Count==0 && state.ActionReveals.Count==0,
            "coûts de main : refus sans énergie dépensée, dévoilement ni défausse");
        var automatic=GameHandCosts.AutoChoose(state.PlayerOne,source,source.Abilities[0].Conditions);
        check(GameHandCosts.Validate(state.PlayerOne,source,source.Abilities[0].Conditions,automatic).Count==3,
            "coûts de main : l'IA choisit des cartes distinctes malgré les filtres qui se chevauchent");
        await engine.UseAbilityAsync(state,"a",source.InstanceId,1,null,handCosts:Choices(second.InstanceId,discarded.InstanceId));
        check(state.PlayerOne.Money==106 && state.PlayerOne.Hand.SequenceEqual(new[]{first,second}) && state.PlayerOne.Graveyard.Single()==discarded && discarded.Zone==GameCardZone.Graveyard,
            "coûts de main : paiement et capacité appliqués ensemble après confirmation");
        check(state.ActionReveals.SequenceEqual(new[]{first,second}) && !JsonSerializer.Serialize(state).Contains("ActionReveals"),
            "coûts de main : cartes dévoilées conservées en main et révélation temporaire non persistée");
        var play=Card("a");play.Type=CardType.PoliticalDecision;play.Abilities.Add(new(){Type=AbilityType.Effect,
            Conditions=new(){new(){Type=GameConditionType.DiscardHandCards,Value=1}},Effects=new(){new(){Type=GameEffectType.GainMoney,Trigger=GameEffectTrigger.OnPlay,Target=GameTargetType.FriendlyPlayer,Value=5}}});
        state.PlayerOne.Hand.Add(play);
        check(GameHandCosts.Requirements(state.PlayerOne,play,play.Abilities[0].Conditions)[0].EligibleIds.All(id=>id!=play.InstanceId),
            "coûts de main : carte jouée exclue des cartes à payer");
        await engine.PlayCardAsync(state,"a",play.InstanceId,null,handCosts:new[]{new GameHandCostSelection{ConditionIndex=0,CardIds=new(){first.InstanceId}}});
        check(state.PlayerOne.Hand.SequenceEqual(new[]{second}) && state.PlayerOne.Graveyard.Contains(first) && state.PlayerOne.Graveyard.Contains(play),
            "coûts de main : décision défausse la carte choisie puis effectue son action et rejoint le cimetière");
        input.Conditions[0].Negate=true;check(CardAbilityEditor.Validate(CardType.Character,input).Any(),"coûts de main : un paiement ne peut pas être inversé");
        input.Conditions[0].Negate=false;input.Effects[0].Trigger=GameEffectTrigger.Passive;check(CardAbilityEditor.Validate(CardType.Character,input).Any(),"coûts de main : paiement interdit pour les déclenchements automatiques");
    }
}
