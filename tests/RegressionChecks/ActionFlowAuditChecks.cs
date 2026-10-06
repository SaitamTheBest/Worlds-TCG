using web_tcg.Data.Enums;
using web_tcg.Game.Effects;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;
using web_tcg.Models.Admin;
using web_tcg.Services;

public static class ActionFlowAuditChecks
{
    static GameCardState Unit(string owner, CardType type = CardType.Character) => new() {
        OwnerUserId=owner, ControllerUserId=owner, Name=owner, Type=type,
        Zone=type==CardType.Leader ? GameCardZone.Leader : GameCardZone.CharacterBoard,
        RegionId=1, CountryId=1, PoliticalPartyId=1, BaseHealth=100, MaxHealth=100, CurrentHealth=40 };
    static GameState State() => new() { Status=GameMatchStatus.InProgress, ActivePlayerId="a", FirstPlayerId="a", TurnNumber=5,
        PlayerOne=new() { UserId="a", Money=100, TurnsPlayed=3, Leader=Unit("a",CardType.Leader), Characters=new() { Unit("a"),null,null,null } },
        PlayerTwo=new() { UserId="b", Money=100, TurnsPlayed=3, Leader=Unit("b",CardType.Leader), Characters=new() { Unit("b"),null,null,null }, Location=new() { Type=CardType.Location,OwnerUserId="b",ControllerUserId="b",Zone=GameCardZone.Location } } };
    public static async Task RunAsync(Action<bool,string> check)
    {
        var handlers=typeof(IGameEffectHandler).Assembly.GetTypes().Where(t=>!t.IsAbstract && typeof(IGameEffectHandler).IsAssignableFrom(t)).Select(t=>(IGameEffectHandler)Activator.CreateInstance(t)!);
        var engine=new GameEngine(new GameEffectEngine(handlers,new GameTargetResolver()),new GameConditionEvaluator());
        var state=State(); var source=state.PlayerOne.Leader; var target=state.PlayerTwo.Characters[0]!;
        var hand=Unit("a"); hand.Zone=GameCardZone.Hand; state.PlayerOne.Hand.Add(hand);
        foreach(var type in Enum.GetValues<GameConditionType>()) {
            var condition=new GameConditionDefinition { Type=type, Value=type switch {
                GameConditionType.SourceHealthBelow or GameConditionType.TargetHealthBelow=>60,
                GameConditionType.SourceHealthAbove or GameConditionType.TargetHealthAbove=>20,
                _=>1 }, Parameters=new() { ["regions"]="1",["countries"]="1",["parties"]="1",["types"]="2" } };
            var evaluator=new GameConditionEvaluator();
            var accepted=evaluator.AreMet(state,state.PlayerOne,source,target,new[]{condition});
            condition.Negate=true;
            check(accepted && !evaluator.AreMet(state,state.PlayerOne,source,target,new[]{condition}) && evaluator.Failure(state,state.PlayerOne,source,target,new[]{condition})!=null,
                $"audit conditions : {type} et sa négation sont évalués en jeu");
        }

        // Opening triggers must obey conditions, unlike a card play already validated before its payment.
        state=State(); state.PlayerOne.Money=state.PlayerTwo.Money=0;
        state.PlayerOne.TurnsPlayed=state.PlayerTwo.TurnsPlayed=0;
        source=state.PlayerOne.Leader;
        source.Abilities.Add(new() { Type=AbilityType.Effect, Conditions=new() { new() { Type=GameConditionType.PlayerMoneyAtLeast,Value=999 } }, Effects=new() {
            new() { Type=GameEffectType.HealthBonus,Trigger=GameEffectTrigger.OnPlay,Target=GameTargetType.Self,Value=20 } } });
        await engine.StartMatchAsync(state);
        check(source.MaxHealth==100,"audit déclenchements : condition du Leader respectée au lancement");
        check(state.GetPlayer(state.FirstPlayerId).Money==30,"énergie : premier tour du premier joueur augmenté de 10 à 30");
        var next=state.GetOpponent(state.ActivePlayerId);
        var logs=await engine.EndTurnAsync(state,state.ActivePlayerId);
        check(next.Money==40 && logs.Any(l=>l.Contains("gagne 40 énergie")),"énergie : premier tour du second joueur augmenté de 20 à 40");
        var opening=state.GetOpponent(state.ActivePlayerId);
        await engine.EndTurnAsync(state,state.ActivePlayerId);
        check(opening.Money==70,"énergie : tours suivants accordent 40 énergie");
        check(new GameStateProjector().Project(state,"a").FirstPlayerId==state.FirstPlayerId,
            "pile ou face : le résultat serveur est transmis aux deux joueurs");
        var capped = state.GetOpponent(state.ActivePlayerId); capped.Money=740;
        var capLogs = await engine.EndTurnAsync(state,state.ActivePlayerId);
        check(capped.Money==750 && capLogs.Any(l=>l.Contains("gagne 10 énergie")),
            "énergie : gain du tour plafonné à 750 et quantité réellement gagnée annoncée");
        var energyContext=new GameEffectContext { State=state,Player=capped,Source=capped.Leader };
        await new GainMoneyEffectHandler().ExecuteAsync(energyContext,new() { Target=GameTargetType.FriendlyPlayer,Value=100 });
        check(capped.Money==750 && energyContext.Logs.Any(l=>l.Contains("gagne 0 énergie")),
            "énergie : capacité de gain respecte le plafond");
        capped.Money=745; var donor=state.GetOpponent(capped.UserId); donor.Money=100;
        await new StealMoneyEffectHandler().ExecuteAsync(energyContext,new() { Target=GameTargetType.EnemyPlayer,Value=20 });
        check(capped.Money==750 && donor.Money==95,"énergie : vol limité à la place disponible sans détruire le surplus adverse");

        state=State(); source=Unit("a",CardType.PoliticalDecision); source.Zone=GameCardZone.Hand; source.UsageCost=100;
        source.Abilities.Add(new() { Type=AbilityType.Effect, Conditions=new() { new() { Type=GameConditionType.PlayerMoneyAtLeast,Value=100 } }, Effects=new() {
            new() { Type=GameEffectType.Heal,Trigger=GameEffectTrigger.OnPlay,Target=GameTargetType.FriendlyLeader,Value=20 } } });
        state.PlayerOne.Hand.Add(source);
        await engine.PlayCardAsync(state,"a",source.InstanceId,null);
        check(state.PlayerOne.Money==0 && state.PlayerOne.Leader.CurrentHealth==60 && state.PlayerOne.Graveyard.Contains(source),
            "audit pose : condition d'énergie validée avant paiement reste acquise après paiement");

        state=State(); source=state.PlayerOne.Leader; target=state.PlayerTwo.Characters[0]!; target.IsStealthed=true;
        source.Abilities.Add(new() { Id=1,Type=AbilityType.Skill,UsageCost=10,Effects=new() {
            new() { Type=GameEffectType.Prison,Trigger=GameEffectTrigger.OnSkill,Target=GameTargetType.EnemyCharacter,DurationTurns=1 } } });
        try { await engine.UseAbilityAsync(state,"a",source.InstanceId,1,target.InstanceId); check(false,"audit camouflage : Prison ciblée refusée"); }
        catch(GameRuleException) { check(state.PlayerOne.Money==100 && !source.IsExhausted && !target.IsImprisoned,"audit camouflage : Prison ciblée refusée sans paiement ni épuisement"); }
        target.IsStealthed=false;
        await engine.UseAbilityAsync(state,"a",source.InstanceId,1,target.InstanceId);
        check(target.IsImprisoned && source.IsExhausted && state.PlayerOne.Money==90,"audit camouflage : Prison fonctionne sur une cible visible");

        state=State();
        state.PlayerOne.Leader.Abilities.Add(new() { Type=AbilityType.Effect,Effects=new() {
            new() { Type=GameEffectType.GainMoney,Trigger=GameEffectTrigger.OnTurnEnd,Target=GameTargetType.FriendlyPlayer,Value=7 } } });
        state.PlayerTwo.Leader.Abilities.Add(new() { Type=AbilityType.Effect,Effects=new() {
            new() { Type=GameEffectType.GainMoney,Trigger=GameEffectTrigger.OnTurnStart,Target=GameTargetType.FriendlyPlayer,Value=9 } } });
        await engine.EndTurnAsync(state,"a");
        check(state.PlayerOne.Money==107 && state.PlayerTwo.Money==149,"audit déclenchements : fin du tour puis début adverse exécutés une seule fois");

        state=State(); source=state.PlayerOne.Leader; target=state.PlayerTwo.Characters[0]!;
        source.Abilities.Add(new() { Id=1,Type=AbilityType.Trait,Effects=new() {
            new() { Type=GameEffectType.Damage,Trigger=GameEffectTrigger.OnTalent,Target=GameTargetType.EnemyCharacter,Value=10 } } });
        target.Abilities.Add(new() { Type=AbilityType.Effect,Effects=new() {
            new() { Type=GameEffectType.GainMoney,Trigger=GameEffectTrigger.OnDamaged,Target=GameTargetType.FriendlyPlayer,Value=7 } } });
        target.HasShield=true;
        await engine.UseAbilityAsync(state,"a",source.InstanceId,1,target.InstanceId);
        check(target.CurrentHealth==40 && state.PlayerTwo.Money==100 && !target.HasShield,"audit réaction : bouclier absorbe l'attaque sans déclencher une réaction aux dégâts");
        await engine.UseAbilityAsync(state,"a",source.InstanceId,1,target.InstanceId);
        check(target.CurrentHealth==30 && state.PlayerTwo.Money==107,"audit réaction : dégâts reçus déclenchent l'effet du bénéficiaire");
        target.Abilities.Add(new() { Type=AbilityType.Effect,Effects=new() {
            new() { Type=GameEffectType.Damage,Trigger=GameEffectTrigger.OnDeath,Target=GameTargetType.EnemyLeader,Value=10 } } });
        state.PlayerOne.Leader.IsStealthed=true;
        state.PlayerOne.Leader.AppliedEffects.Add(new() { Type=GameEffectType.Stealth,Value=1 });
        source.Abilities[0].Effects[0].Value=100;
        await engine.UseAbilityAsync(state,"a",source.InstanceId,1,target.InstanceId);
        check(state.PlayerTwo.Characters[0]==null && state.PlayerTwo.Graveyard.Contains(target) && source.CurrentHealth==40,
            "audit mort : Dernier souffle avec cible camouflée ne bloque pas l'envoi au cimetière");

        state=State(); source=state.PlayerOne.Leader; target=state.PlayerOne.Characters[0]!;
        source.Abilities.Add(new() { Id=1,Type=AbilityType.Trait,Effects=new() {
            new() { Type=GameEffectType.HealthBonus,Trigger=GameEffectTrigger.OnTalent,Target=GameTargetType.FriendlyCharacter,Value=20,DurationTurns=1 },
            new() { Type=GameEffectType.DamageBonus,Trigger=GameEffectTrigger.OnTalent,Target=GameTargetType.FriendlyCharacter,Value=10,DurationTurns=1 } } });
        await engine.UseAbilityAsync(state,"a",source.InstanceId,1,target.InstanceId);
        await engine.EndTurnAsync(state,"a"); await engine.EndTurnAsync(state,"b");
        check(target.MaxHealth==120 && target.DamageBonus==10,"audit durée : bonus actifs pendant le prochain tour du bénéficiaire");
        await engine.EndTurnAsync(state,"a"); await engine.EndTurnAsync(state,"b");
        check(target.MaxHealth==100 && target.CurrentHealth==40 && target.DamageBonus==0,"audit durée : PV maximum et dégâts reviennent à leur valeur initiale après expiration");

        var input=new CardAbilityInputModel { Type=AbilityType.Effect, Effects=new() {
            new() { Type=GameEffectType.Heal,Trigger=GameEffectTrigger.Passive,Target=GameTargetType.Self,Value=20 } } };
        check(CardAbilityEditor.Validate(CardType.Character,input).Any(),"audit éditeur : soin continu ambigu refusé, déclenchement explicite requis");
        input.Effects[0].Type=GameEffectType.HealthBonus; input.Effects[0].Target=GameTargetType.FriendlyCharacter;
        check(CardAbilityEditor.Validate(CardType.Character,input).Any(),"audit éditeur : passif exigeant un choix de cible refusé");
        input.Effects[0].Target=GameTargetType.AllFriendlyCharacters;
        check(!CardAbilityEditor.Validate(CardType.Character,input).Any(),"audit éditeur : aura de groupe automatique autorisée");
    }
}
