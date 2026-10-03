using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;
using web_tcg.Game.Engine;
using web_tcg.Services;

public static class TargetingChecks
{
    public static void Run(Action<bool,string> check)
    {
        GameCardState Unit(string owner,int region = 1) => new() { Name="Carte test",ControllerUserId=owner,OwnerUserId=owner,Type=CardType.Character,RegionId=region,CurrentHealth=100,MaxHealth=100,Zone=GameCardZone.CharacterBoard };
        var source = Unit("a");
        var enemy = Unit("b"); var hidden = Unit("b"); hidden.IsStealthed = true;
        var state = new GameState { Status=GameMatchStatus.InProgress,ActivePlayerId="a",TurnNumber=5,
            PlayerOne = new() { UserId="a",Leader=Unit("a"),TurnsPlayed=2,Money=10 },
            PlayerTwo = new() { UserId="b",Leader=Unit("b"),TurnsPlayed=2 } };
        state.PlayerOne.Characters[0]=source; state.PlayerTwo.Characters[0]=enemy; state.PlayerTwo.Characters[1]=hidden;
        var ability = new GameAbilityState { Id=1,Type=AbilityType.Skill,Effects=new() { new() { Type=GameEffectType.Damage,Target=GameTargetType.EnemyCharacterOrLeader,Trigger=GameEffectTrigger.OnSkill,Value=20 } } };
        source.Abilities.Add(ability);
        var ids = GameActionTargets.Get(state,state.PlayerOne,source,new[] {ability},GameEffectTrigger.OnSkill);
        check(ids.Contains(enemy.InstanceId) && ids.Contains(state.PlayerTwo.Leader.InstanceId) && !ids.Contains(hidden.InstanceId) && !ids.Contains(source.InstanceId),"ciblage : attaques limitées aux adversaires visibles");
        enemy.IsBlocking=true; enemy.BlockExpiresAfterTurn=6;
        ids=GameActionTargets.Get(state,state.PlayerOne,source,new[] {ability},GameEffectTrigger.OnSkill);
        check(ids.SequenceEqual(new[] {enemy.InstanceId}),"ciblage : seul le bloqueur est proposé quand il protège son équipe");
        enemy.IsBlocking=false; ability.Effects[0].Parameters["regions"]="2";
        enemy.RegionId=2;
        check(GameActionTargets.Get(state,state.PlayerOne,source,new[] {ability},GameEffectTrigger.OnSkill).SequenceEqual(new[] {enemy.InstanceId}),"ciblage : filtres d'origine appliqués aux cartes proposées");
        ability.Effects[0].Parameters.Clear(); ability.Conditions.Add(new() { Type=GameConditionType.TargetHealthBelow,Value=50 });
        enemy.CurrentHealth=40;
        var dto = new GameStateProjector().Project(state,"a").Me.Characters[0]!.Abilities[0];
        check(dto.IsAttack && dto.TargetIds.SequenceEqual(new[] {enemy.InstanceId}) && dto.UnavailableReason == null,"ciblage : condition de PV de la cible et indication d'attaque transmises");
        ability.Conditions.Add(new() {Type=GameConditionType.PlayerMoneyAtLeast,Value=20});
        var failure = new GameConditionEvaluator().Failure(state,state.PlayerOne,source,enemy,ability.Conditions);
        check(failure != null && failure.Contains("20") && failure.Contains("actuellement 10"),"conditions : refus indique le seuil requis et la valeur actuelle");
    }
}
