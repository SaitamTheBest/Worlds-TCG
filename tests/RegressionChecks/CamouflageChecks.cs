using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Effects;
using web_tcg.Game.Runtime;
using web_tcg.Services;

public static class CamouflageChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        GameCardState Card(string owner,CardType type=CardType.Character) => new() { OwnerUserId=owner,ControllerUserId=owner,Name="Camouflage test",Type=type,
            Zone=type==CardType.Leader?GameCardZone.Leader:GameCardZone.CharacterBoard,CurrentHealth=100,BaseHealth=100,MaxHealth=100 };
        var state=new GameState {Status=GameMatchStatus.InProgress,ActivePlayerId="a",TurnNumber=5,
            PlayerOne=new() {UserId="a",Leader=Card("a",CardType.Leader),TurnsPlayed=3,Money=100},
            PlayerTwo=new() {UserId="b",Leader=Card("b",CardType.Leader),TurnsPlayed=3,Money=100}};
        var hidden=Card("b");hidden.IsStealthed=true;hidden.AppliedEffects.Add(new() {Type=GameEffectType.Stealth,Value=1});
        state.PlayerTwo.Characters[0]=hidden;
        var attacker=state.PlayerOne.Leader;
        var ability=new GameAbilityState {Id=1,Type=AbilityType.Skill,Effects=new() {
            new() {Type=GameEffectType.Damage,Trigger=GameEffectTrigger.OnSkill,Target=GameTargetType.EnemyCharacter,Value=10}}};
        attacker.Abilities.Add(ability);
        bool CanTarget() => GameActionTargets.Get(state,state.PlayerOne,attacker,new[]{ability},GameEffectTrigger.OnSkill).Contains(hidden.InstanceId);
        bool Displayed() => new GameStateProjector().Project(state,"a").Opponent.Characters[0]!.IsStealthed;
        check(!GameCamouflage.IsActive(state,hidden)&&CanTarget()&&!Displayed(),"camouflage : personnage seul ciblable, état inactif dans Play");
        state.PlayerTwo.Location=Card("b",CardType.Location);state.PlayerTwo.Location.Zone=GameCardZone.Location;
        state.PlayerOne.Characters[0]=Card("a");
        check(CanTarget()&&!Displayed(),"camouflage : Leader, Lieu et personnages adverses ne comptent pas");
        var support=Card("b");support.IsExhausted=true;state.PlayerTwo.Characters[1]=support;
        check(!CanTarget()&&Displayed(),"camouflage : deuxième personnage allié protège même s'il est épuisé");
        support.CurrentHealth=0;
        check(CanTarget()&&!Displayed(),"camouflage : personnage mort ne protège plus avant même son déplacement au cimetière");
        support.CurrentHealth=100;support.IsKnockedOut=true;
        check(CanTarget(),"camouflage : personnage KO ne protège pas");
        support.IsKnockedOut=false;support.CurrentHealth=5;
        var engine=new GameEngine(new GameEffectEngine(new IGameEffectHandler[]{new DamageEffectHandler()},new GameTargetResolver()),new GameConditionEvaluator());
        await engine.UseAbilityAsync(state,"a",attacker.InstanceId,1,support.InstanceId);
        check(state.PlayerTwo.Characters[1]==null&&CanTarget()&&!Displayed()&&hidden.IsStealthed,
            "camouflage : mort du dernier allié désactive le ciblage protégé sans supprimer l'effet accordé");
        state.PlayerTwo.Characters[1]=Card("b");
        check(!CanTarget()&&Displayed(),"camouflage : arrivée d'un nouveau personnage réactive automatiquement la protection");
        var restored=System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(state))!;
        check(GameCamouflage.IsActive(restored,restored.PlayerTwo.Characters[0]!),"camouflage : règle conservée après sauvegarde et rechargement");
        state.PlayerTwo.Characters[1]=null;state.PlayerTwo.Leader.IsStealthed=true;
        check(GameCamouflage.IsActive(state,state.PlayerTwo.Leader)&&new GameStateProjector().Project(state,"a").Opponent.Leader.IsStealthed,
            "camouflage : Leader conserve sa propre règle explicite même sans personnage");
        state.PlayerTwo.Leader.IsStealthed=false;
        check(!GameCamouflage.IsActive(state,state.PlayerTwo.Leader),"camouflage : Leader sans effet accordé reste visible");
        hidden.Zone=GameCardZone.Hand;
        check(!GameCamouflage.IsActive(state,hidden),"camouflage : effet d'un personnage hors plateau inactif");
    }
}
