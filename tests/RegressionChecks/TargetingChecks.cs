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
        ability.Conditions.Clear();
        ability.Conditions.Add(new() { Type=GameConditionType.TargetRegion,Parameters=new() { ["regions"]="2" } });
        ability.Effects.Add(new() { Type=GameEffectType.Heal,Target=GameTargetType.Self,Trigger=GameEffectTrigger.OnSkill,Value=10 });
        ability.Effects.Add(new() { Type=GameEffectType.GainMoney,Target=GameTargetType.FriendlyPlayer,Trigger=GameEffectTrigger.OnSkill,Value=10 });
        dto = new GameStateProjector().Project(state,"a").Me.Characters[0]!.Abilities[0];
        check(dto.TargetIds.SequenceEqual(new[] {enemy.InstanceId}) && dto.TargetRefusalReason == null,
            "ciblage : attaque Europe avec soin personnel et énergie conserve sa cible adverse");
        enemy.RegionId=3;
        check(GameActionTargets.Refusal(state,state.PlayerOne,source,ability,GameEffectTrigger.OnSkill)?.Contains("région de la cible") == true,
            "ciblage : refus indique la condition régionale non remplie");
        enemy.RegionId=2; enemy.IsStealthed=true;
        check(GameActionTargets.Refusal(state,state.PlayerOne,source,ability,GameEffectTrigger.OnSkill)?.Contains("camoufl",StringComparison.OrdinalIgnoreCase) == true,
            "ciblage : refus explique le camouflage de la cible régionale");
        enemy.IsStealthed=false;
        ability.Conditions.Clear(); ability.Effects.RemoveRange(1,2);
        var preview = GameActionPreviewBuilder.Create(state, "a", source.InstanceId, 1, enemy.InstanceId, null);
        check(preview.SourceId == source.InstanceId && preview.TargetId == enemy.InstanceId && preview.IsAttack && !preview.Confirmed,
            "préparation : source et cible validées sans effectuer l'attaque");
        check(enemy.CurrentHealth == 40 && state.PlayerOne.Money == 10 && !source.IsExhausted,
            "préparation : aucun dégât, paiement ou épuisement avant confirmation");
        bool Refused(Guid id, int? abilityId, Guid? target = null) {
            try { GameActionPreviewBuilder.Create(state, "a", id, abilityId, target, null); return false; }
            catch (GameRuleException) { return true; }
        }
        check(Refused(source.InstanceId, 1, hidden.InstanceId), "préparation : cible camouflée refusée");
        check(Refused(enemy.InstanceId, 1), "préparation : impossible de sélectionner la carte d'un autre joueur");
        source.IsExhausted = true;
        check(Refused(source.InstanceId, 1), "préparation : capacité épuisée refusée"); source.IsExhausted = false;
        var hand = Unit("a"); hand.Zone = GameCardZone.Hand; hand.Name = "Carte secrète en main";
        state.PlayerOne.Hand.Add(hand);
        preview = GameActionPreviewBuilder.Create(state, "a", hand.InstanceId, null, null, 1);
        check(preview.SourceId == null && preview.Slot == 1 && !preview.Label.Contains(hand.Name),
            "préparation : emplacement visible sans révéler la carte encore en main");
        state.ActivePlayerId = "b";
        check(Refused(source.InstanceId, 1), "préparation : sélection refusée hors tour");
        check(GameActionPreviewBuilder.Create(state, "a", null, null, null, null).Label == "",
            "préparation : annulation possible même après changement de tour");
    }
}
