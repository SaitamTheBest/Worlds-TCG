using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Effects;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;
using web_tcg.Models.Admin;
using web_tcg.Services;

public static class OrientationRuleChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        GameCardState Unit(string owner,int? party,PoliticalOrientation? orientation,CardType type=CardType.Character) => new() {
            OwnerUserId=owner,ControllerUserId=owner,PoliticalPartyId=party,PoliticalPartyOrientation=orientation,Type=type,
            Zone=type==CardType.Leader ? GameCardZone.Leader : GameCardZone.CharacterBoard,BaseHealth=100,MaxHealth=100,CurrentHealth=100 };
        var first=Unit("a",1,PoliticalOrientation.Left); var second=Unit("a",2,PoliticalOrientation.Left);
        var centre=Unit("a",3,PoliticalOrientation.Centre); var neutral=Unit("a",null,null);
        var enemy=Unit("b",4,PoliticalOrientation.Left);
        var state=new GameState { Status=GameMatchStatus.InProgress,ActivePlayerId="a",PlayerOne=new() { UserId="a",TurnsPlayed=3,Money=100,
            Leader=Unit("a",1,PoliticalOrientation.Left,CardType.Leader),Characters=new() { first,second,centre,neutral } },
            PlayerTwo=new() { UserId="b",TurnsPlayed=3,Leader=Unit("b",3,PoliticalOrientation.Centre,CardType.Leader),Characters=new() { enemy,null,null,null } } };
        var condition=new GameConditionDefinition { Type=GameConditionType.TargetPoliticalOrientation,Parameters=new() { ["orientations"]="2" } };
        var evaluator=new GameConditionEvaluator();
        check(evaluator.AreMet(state,state.PlayerOne,first,second,new[]{condition}) && !evaluator.AreMet(state,state.PlayerOne,first,centre,new[]{condition})
            && !evaluator.AreMet(state,state.PlayerOne,first,neutral,new[]{condition}),"orientations : partis distincts de gauche acceptés, centre et sans parti exclus");
        check(evaluator.AreMet(state,state.PlayerOne,first,null,new[]{condition}),"orientations : condition de cible différée jusqu'au choix du bénéficiaire");
        condition.Type=GameConditionType.SourcePoliticalOrientation;
        check(evaluator.AreMet(state,state.PlayerOne,first,null,new[]{condition}) && !evaluator.AreMet(state,state.PlayerOne,centre,null,new[]{condition}),"orientations : condition de source intégrée");
        var input=new CardAbilityInputModel { Type=AbilityType.Skill,Effects=new() { new() {
            Type=GameEffectType.DamageBonus,Trigger=GameEffectTrigger.OnSkill,Target=GameTargetType.AllCharacters,Value=20,
            OrientationIds=new() {2},CountOrientationIds=new() {2},CountScope=3,ScaleByMatchingCharacters=true } },
            Conditions=new() {new() { Type=GameConditionType.TargetPoliticalOrientation,OrientationIds=new() {2} }} };
        var stored=new CardAbility(); CardAbilityEditor.Write(stored,input); var restored=CardAbilityEditor.Read(stored);
        check(restored.Effects[0].OrientationIds.SequenceEqual(new[]{2}) && restored.Effects[0].CountOrientationIds.SequenceEqual(new[]{2}) && restored.Conditions[0].OrientationIds.SequenceEqual(new[]{2}),
            "orientations : filtres, comptages et conditions conservés en modification");
        check(!CardAbilityEditor.Validate(CardType.Character,input).Any(),"orientations : bonus de groupe par personnage de gauche valide");
        var definition=new GameEffectDefinition { Type=GameEffectType.DamageBonus,Trigger=GameEffectTrigger.OnSkill,Target=GameTargetType.AllCharacters,Value=20,
            Parameters=new() { ["orientations"]="2",["countorientations"]="2",["scaleByCount"]="true",["countScope"]="3" } };
        var handlers=typeof(IGameEffectHandler).Assembly.GetTypes().Where(t=>!t.IsAbstract && typeof(IGameEffectHandler).IsAssignableFrom(t)).Select(t=>(IGameEffectHandler)Activator.CreateInstance(t)!);
        var effectEngine=new GameEffectEngine(handlers,new GameTargetResolver());
        await effectEngine.ExecuteAsync(new() { State=state,Player=state.PlayerOne,Source=state.PlayerOne.Leader },new[]{definition},GameEffectTrigger.OnSkill);
        check(first.DamageBonus==60 && second.DamageBonus==60 && enemy.DamageBonus==60 && centre.DamageBonus==0 && neutral.DamageBonus==0,
            "orientations : bonus de 20 multiplié par les trois personnages de partis de gauche");
        definition.Parameters["orientations"]="2,3";
        var resolver=new GameTargetResolver();
        check(resolver.ResolveCards(state,state.PlayerOne,first,definition,null).Count==4,"orientations : plusieurs orientations combinées avec OU sans inclure les Leaders");
        definition.Parameters["parties"]="2";
        check(resolver.ResolveCards(state,state.PlayerOne,first,definition,null).Single()==second,"orientations : orientation et parti combinés avec ET");
        var skill=new GameAbilityState { Type=AbilityType.Skill,Conditions=new() { condition },Effects=new() { new() {
            Type=GameEffectType.Damage,Trigger=GameEffectTrigger.OnSkill,Target=GameTargetType.EnemyCharacterOrLeader,Value=10 } } };
        condition.Type=GameConditionType.TargetPoliticalOrientation;
        check(GameActionTargets.Get(state,state.PlayerOne,first,new[]{skill},GameEffectTrigger.OnSkill).SequenceEqual(new[]{enemy.InstanceId}),
            "orientations : Play propose uniquement les cibles adverses de gauche");
        var snapshot=System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(state))!;
        check(snapshot.PlayerOne.Characters[1]!.PoliticalPartyOrientation==PoliticalOrientation.Left,"orientations : sauvegarde de partie conserve l'orientation");
        input.Conditions[0].OrientationIds.Clear();
        check(CardAbilityEditor.Validate(CardType.Character,input).Any(),"orientations : condition sans orientation refusée");
        input.Conditions[0].OrientationIds.Add(99);
        check(CardAbilityEditor.Validate(CardType.Character,input).Any(),"orientations : orientation inconnue refusée");
        var leaderCondition = new GameConditionDefinition { Type = GameConditionType.LeaderPoliticalOrientation, Parameters = new() { ["orientations"] = "2" } };
        check(evaluator.AreMet(state,state.PlayerOne,centre,enemy,new[]{leaderCondition}),
            "orientation du Leader : utilise le Leader allié même si la source est du centre");
        check(!evaluator.AreMet(state,state.PlayerTwo,enemy,first,new[]{leaderCondition}) && !GameConditionEvaluator.IsTargetCondition(leaderCondition),
            "orientation du Leader : condition immédiate indépendante de la cible et du Leader adverse");
        leaderCondition.Parameters["orientations"] = "3,2";
        check(evaluator.AreMet(state,state.PlayerTwo,enemy,null,new[]{leaderCondition}),"orientation du Leader : plusieurs orientations acceptées avec OU");
        state.PlayerOne.Leader.PoliticalPartyId = null;
        check(!evaluator.AreMet(state,state.PlayerOne,centre,null,new[]{leaderCondition}),"orientation du Leader : Leader sans parti refusé");
        state.PlayerOne.Leader.PoliticalPartyId = 1;
        state.PlayerOne.Leader.CurrentHealth = 0;
        check(!evaluator.AreMet(state,state.PlayerOne,centre,null,new[]{leaderCondition}),"orientation du Leader : Leader mort refusé");
        state.PlayerOne.Leader.CurrentHealth = 100;
        leaderCondition.Parameters["orientations"] = "3";
        leaderCondition.Negate = true;
        check(evaluator.AreMet(state,state.PlayerOne,centre,null,new[]{leaderCondition}),"orientation du Leader : inversion de condition prise en compte");
        leaderCondition.Negate = false;
        check(evaluator.Failure(state,state.PlayerOne,centre,null,new[]{leaderCondition})?.Contains("votre Leader") == true,
            "orientation du Leader : erreur précise affichable en jeu");
        input.Conditions[0].Type = GameConditionType.LeaderPoliticalOrientation;
        input.Conditions[0].OrientationIds = new() { 2 };
        CardAbilityEditor.Write(stored,input); restored = CardAbilityEditor.Read(stored);
        check(restored.Conditions[0].Type == GameConditionType.LeaderPoliticalOrientation && restored.Conditions[0].OrientationIds.SequenceEqual(new[]{2})
            && !CardAbilityEditor.Validate(CardType.Character,input).Any(),"orientation du Leader : création et modification conservent la condition");
        input.Conditions[0].OrientationIds.Clear();
        check(CardAbilityEditor.Validate(CardType.Character,input).Any(),"orientation du Leader : sélection d'une orientation obligatoire");
        var enemyCondition = new GameConditionDefinition { Type = GameConditionType.EnemyLeaderPoliticalOrientation,
            Parameters = new() { ["orientations"] = "3" } };
        check(evaluator.AreMet(state,state.PlayerOne,first,null,new[]{enemyCondition})
            && !evaluator.AreMet(state,state.PlayerTwo,enemy,null,new[]{enemyCondition}),
            "orientation du Leader adverse : le camp adverse est évalué indépendamment de la cible");
        enemyCondition.Parameters["orientations"] = "5";
        enemyCondition.Negate = true;
        check(evaluator.AreMet(state,state.PlayerOne,first,null,new[]{enemyCondition}),
            "orientation du Leader adverse : condition non extrême droite acceptée");
        state.PlayerTwo.Leader.PoliticalPartyOrientation = PoliticalOrientation.FarRight;
        check(!evaluator.AreMet(state,state.PlayerOne,first,null,new[]{enemyCondition})
            && evaluator.Failure(state,state.PlayerOne,first,null,new[]{enemyCondition})?.Contains("Leader adverse") == true,
            "orientation du Leader adverse : inversion exclut extrême droite avec explication");
        input.Conditions[0].Type = GameConditionType.EnemyLeaderPoliticalOrientation;
        input.Conditions[0].OrientationIds = new() { 5 };
        input.Conditions[0].Negate = true;
        CardAbilityEditor.Write(stored,input); restored = CardAbilityEditor.Read(stored);
        check(restored.Conditions[0].Type == GameConditionType.EnemyLeaderPoliticalOrientation && restored.Conditions[0].Negate
            && restored.Conditions[0].OrientationIds.SequenceEqual(new[]{5}) && !CardAbilityEditor.Validate(CardType.Character,input).Any(),
            "orientation du Leader adverse : édition conserve orientation et négation");
        input.Conditions[0].OrientationIds.Clear();
        check(CardAbilityEditor.Validate(CardType.Character,input).Any(),"orientation du Leader adverse : sélection obligatoire");

        var attacker = Unit("a",1,PoliticalOrientation.Left); attacker.Zone = GameCardZone.Hand;
        attacker.Abilities.Add(new() { Id=90,Type=AbilityType.Effect,Conditions=new() { enemyCondition },Effects=new() {
            new() { Type=GameEffectType.DamageBonus,Trigger=GameEffectTrigger.Passive,Target=GameTargetType.Self,Value=40 } } });
        attacker.Abilities.Add(new() { Id=91,Type=AbilityType.Skill,Effects=new() {
            new() { Type=GameEffectType.Damage,Trigger=GameEffectTrigger.OnSkill,Target=GameTargetType.EnemyCharacter,Value=10 } } });
        var victim = Unit("b",2,PoliticalOrientation.Right);
        var bonusState = new GameState { Status=GameMatchStatus.InProgress,ActivePlayerId="a",TurnNumber=5,
            PlayerOne=new() { UserId="a",TurnsPlayed=3,Money=100,Leader=Unit("a",1,PoliticalOrientation.FarRight,CardType.Leader),Hand=new() { attacker } },
            PlayerTwo=new() { UserId="b",TurnsPlayed=3,Leader=Unit("b",3,PoliticalOrientation.Centre,CardType.Leader),Characters=new() { victim,null,null,null } } };
        var game = new GameEngine(effectEngine,evaluator);
        await game.PlayCardAsync(bonusState,"a",attacker.InstanceId,0);
        attacker.IsExhausted=false;
        await game.UseAbilityAsync(bonusState,"a",attacker.InstanceId,91,victim.InstanceId);
        check(attacker.DamageBonus==40 && victim.CurrentHealth==50,
            "orientation du Leader adverse : bonus passif de 40 ajouté aux dégâts de la compétence");
        bonusState.PlayerTwo.Leader.PoliticalPartyOrientation=PoliticalOrientation.FarRight;
        await game.EndTurnAsync(bonusState,"a");
        await game.EndTurnAsync(bonusState,"b");
        victim.CurrentHealth=100;
        await game.UseAbilityAsync(bonusState,"a",attacker.InstanceId,91,victim.InstanceId);
        check(attacker.DamageBonus==0 && victim.CurrentHealth==90,
            "orientation du Leader adverse : bonus retiré quand l'orientation devient extrême droite");
    }
}
