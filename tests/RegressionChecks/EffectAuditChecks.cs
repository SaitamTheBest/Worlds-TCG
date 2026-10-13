using web_tcg.Data.Enums;
using web_tcg.Game.Effects;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;

public static class EffectAuditChecks
{
    static GameCardState Unit(string owner, CardType type = CardType.Character) => new() {
        OwnerUserId = owner, ControllerUserId = owner, Name = owner + " carte", Type = type,
        Zone = type == CardType.Leader ? GameCardZone.Leader : GameCardZone.CharacterBoard,
        BaseHealth = 100, MaxHealth = 100, CurrentHealth = 40 };
    static GameState State() => new() { Status = GameMatchStatus.InProgress, ActivePlayerId = "a", TurnNumber = 5,
        PlayerOne = new() { UserId = "a", DisplayName = "A", TurnsPlayed = 3, Money = 100, Leader = Unit("a", CardType.Leader), Characters = new() { Unit("a"), Unit("a"), null, null } },
        PlayerTwo = new() { UserId = "b", DisplayName = "B", TurnsPlayed = 3, Money = 15, Leader = Unit("b", CardType.Leader), Characters = new() { Unit("b"), null, null, null } } };
    public static async Task RunAsync(Action<bool,string> check)
    {
        var handlers = typeof(IGameEffectHandler).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IGameEffectHandler).IsAssignableFrom(t))
            .Select(t => (IGameEffectHandler)Activator.CreateInstance(t)!).ToArray();
        check(handlers.Select(h => h.EffectType).Order().SequenceEqual(Enum.GetValues<GameEffectType>().Order()),
            "audit : chaque effet défini possède un gestionnaire unique");
        var effects = new GameEffectEngine(handlers, new GameTargetResolver());
        var engine = new GameEngine(effects, new GameConditionEvaluator());
        foreach (var type in Enum.GetValues<GameEffectType>())
        {
            var state = State(); var source = state.PlayerOne.Characters[0]!; var target = state.PlayerTwo.Characters[0]!;
            state.PlayerOne.Deck.Add(Unit("a")); state.PlayerTwo.Hand.Add(Unit("b"));
            state.PlayerTwo.Location = new() { OwnerUserId = "b", ControllerUserId = "b", Type = CardType.Location, Zone = GameCardZone.Location };
            var trigger = type == GameEffectType.Revive ? GameEffectTrigger.OnDeath : GameEffectTrigger.OnSkill;
            var targetType = type switch {
                GameEffectType.DrawCards or GameEffectType.GainMoney => GameTargetType.FriendlyPlayer,
                GameEffectType.LoseMoney or GameEffectType.StealMoney or GameEffectType.DiscardCards => GameTargetType.EnemyPlayer,
                GameEffectType.DestroyLocation => GameTargetType.EnemyLocation,
                GameEffectType.Revive => GameTargetType.Self,
                _ => GameTargetType.EnemyCharacter };
            if (type == GameEffectType.Revive) source.CurrentHealth = 0;
            var definition = new GameEffectDefinition { Type = type, Trigger = trigger, Target = targetType,
                Value = type is GameEffectType.DrawCards or GameEffectType.DiscardCards or GameEffectType.ExtraAttack ? 1 : type == GameEffectType.DamageMultiplier ? 2 : 20,
                DurationTurns = type == GameEffectType.Prison ? 1 : 0 };
            await effects.ExecuteAsync(new() { State = state, Player = state.PlayerOne, Source = source, RequestedTargetId = target.InstanceId }, new[] { definition }, trigger);
            var applied = type switch {
                GameEffectType.Damage => target.CurrentHealth == 20,
                GameEffectType.Heal => target.CurrentHealth == 60,
                GameEffectType.DrawCards => state.PlayerOne.Hand.Count == 1 && state.PlayerOne.Deck.Count == 0,
                GameEffectType.GainMoney => state.PlayerOne.Money == 120,
                GameEffectType.LoseMoney => state.PlayerTwo.Money == 0,
                GameEffectType.StealMoney => state.PlayerTwo.Money == 0 && state.PlayerOne.Money == 115,
                GameEffectType.Block => target.IsBlocking && target.IsExhausted,
                GameEffectType.Shield => target.HasShield,
                GameEffectType.Stealth => target.IsStealthed,
                GameEffectType.Prison => target.IsImprisoned,
                GameEffectType.ExtraAttack => target.ExtraSkillUses == 1,
                GameEffectType.DamageMultiplier => target.DamageMultiplier == 2,
                GameEffectType.HealthBonus => target.MaxHealth == 120 && target.CurrentHealth == 60,
                GameEffectType.DamageBonus => target.DamageBonus == 20,
                GameEffectType.Revive => source.CurrentHealth == 50 && source.HasRevived,
                GameEffectType.DiscardCards => state.PlayerTwo.Hand.Count == 0 && state.PlayerTwo.Graveyard.Count == 1,
                GameEffectType.DestroyLocation => state.PlayerTwo.Location == null && state.PlayerTwo.Graveyard.Count == 1,
                GameEffectType.KnockOut => target.CurrentHealth == 0 && target.IsKnockedOut,
                _ => false };
            check(applied, $"audit : {type} produit son résultat sur sa cible");
        }
        foreach (var allied in new[] { true, false })
        {
            var koState = State(); var actor = koState.PlayerOne.Leader;
            var victimOwner = allied ? koState.PlayerOne : koState.PlayerTwo;
            var victim = victimOwner.Characters[0]!;
            victim.RegionId = 1;
            victim.HasShield = true;
            victim.Abilities.Add(new() { Type = AbilityType.Effect, Effects = new() { new() {
                Type = GameEffectType.Revive, Trigger = GameEffectTrigger.OnDeath, Target = GameTargetType.Self } } });
            actor.Abilities.Add(new() { Id = 99, Type = AbilityType.Trait, Effects = new() { new() {
                Type = GameEffectType.KnockOut, Trigger = GameEffectTrigger.OnTalent,
                Target = allied ? GameTargetType.FriendlyCharacter : GameTargetType.EnemyCharacter } } });
            actor.Abilities[0].Conditions.Add(new() { Type = GameConditionType.TargetRegion,
                Parameters = new() { ["regions"] = "2" } });
            try { await engine.UseAbilityAsync(koState,"a",actor.InstanceId,99,victim.InstanceId); check(false,"KO : condition de région refusée"); }
            catch (GameRuleException) { check(victim.CurrentHealth == 40 && !victim.IsKnockedOut,"KO : condition non remplie conserve la cible sur le terrain"); }
            actor.Abilities[0].Conditions.Clear();
            if (!allied)
            {
                victim.IsStealthed = true;
                try { await engine.UseAbilityAsync(koState,"a",actor.InstanceId,99,victim.InstanceId); check(false,"KO : camouflage respecté"); }
                catch (GameRuleException) { check(!victim.IsKnockedOut,"KO : cible adverse camouflée refusée"); }
                victim.IsStealthed = false;
            }
            await engine.UseAbilityAsync(koState,"a",actor.InstanceId,99,victim.InstanceId);
            check(victimOwner.Characters[0] == null && victimOwner.Graveyard.Contains(victim)
                && victim.Zone == GameCardZone.Graveyard && victim.BoardSlot == null,
                $"KO : personnage {(allied ? "allié" : "adverse")} au cimetière malgré bouclier et second souffle");
            var location = new GameCardState { Type = CardType.Location, OwnerUserId = victimOwner.UserId,
                ControllerUserId = victimOwner.UserId, Zone = GameCardZone.Location };
            victimOwner.Location = location;
            actor.Abilities[0].Effects[0].Target = allied ? GameTargetType.FriendlyLocation : GameTargetType.EnemyLocation;
            await engine.EndTurnAsync(koState,"a");
            await engine.EndTurnAsync(koState,"b");
            await engine.UseAbilityAsync(koState,"a",actor.InstanceId,99,null);
            check(victimOwner.Location == null && victimOwner.Graveyard.Contains(location),
                $"KO : lieu {(allied ? "allié" : "adverse")} retiré du plateau");
        }
        var healState = State(); var healer = healState.PlayerOne.Leader;
        healer.Abilities.Add(new() { Id = 1, Type = AbilityType.Trait, CooldownTurns = 2, Effects = new() {
            new() { Type = GameEffectType.Heal, Trigger = GameEffectTrigger.OnTalent, Target = GameTargetType.FriendlyCharacter, Value = 30 } } });
        var wounded = healState.PlayerOne.Characters[0]!;
        await engine.UseAbilityAsync(healState,"a",healer.InstanceId,1,wounded.InstanceId);
        check(wounded.CurrentHealth == 70 && !healer.IsExhausted && healer.AbilityCooldowns[1] == 2,
            "audit : talent de soin ciblé restaure les PV et démarre sa recharge");
        healer.Abilities.Add(new() { Id = 3, Type = AbilityType.Trait, Conditions = new() {
            new() { Type = GameConditionType.TargetHealthBelow, Value = 80 } }, Effects = new() {
            new() { Type = GameEffectType.Heal, Trigger = GameEffectTrigger.OnTalent, Target = GameTargetType.Self, Value = 30 } } });
        await engine.UseAbilityAsync(healState,"a",healer.InstanceId,3,null);
        check(healer.CurrentHealth == 70, "audit : talent d'auto-soin avec condition de PV cible la carte elle-même");
        healer.Abilities.Add(new() { Id = 4, Type = AbilityType.Trait, UsageCost = 5,
            Conditions = healer.Abilities[1].Conditions, Effects = healer.Abilities[1].Effects });
        healer.CurrentHealth = 90;
        var money = healState.PlayerOne.Money;
        try {
            await engine.UseAbilityAsync(healState,"a",healer.InstanceId,4,null);
            check(false, "audit : auto-soin refusé si la carte ne remplit pas sa condition");
        } catch (GameRuleException ex) {
            check(healState.PlayerOne.Money == money && healer.AbilityCooldowns.GetValueOrDefault(4) == 0 && ex.Message.Contains("90"),
                "audit : auto-soin interdit explique les PV actuels sans paiement ni recharge");
        }
        healer.Abilities.Add(new() { Id = 2, Type = AbilityType.Skill,
            Conditions = new() { new() { Type = GameConditionType.TargetHealthBelow, Value = 60 } }, Effects = new() {
            new() { Type = GameEffectType.Heal, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.AllFriendlyCharacters, Value = 20 } } });
        await engine.UseAbilityAsync(healState,"a",healer.InstanceId,2,null);
        check(wounded.CurrentHealth == 70 && healState.PlayerOne.Characters[1]!.CurrentHealth == 60,
            "audit : soin de groupe conditionné par les PV applique la condition à chaque bénéficiaire");

        var auraState = State(); var aura = Unit("a"); aura.Zone = GameCardZone.Hand;
        aura.Abilities.Add(new() { Type = AbilityType.Effect, Effects = new() {
            new() { Type = GameEffectType.HealthBonus, Trigger = GameEffectTrigger.Passive, Target = GameTargetType.AllFriendlyCharacters, Value = 20 } } });
        auraState.PlayerOne.Hand.Add(aura);
        await engine.PlayCardAsync(auraState,"a",aura.InstanceId,2);
        check(auraState.PlayerOne.Characters.Take(3).All(c => c!.MaxHealth == 120) && auraState.PlayerOne.Leader.MaxHealth == 100,
            "audit : aura de PV maximum appliquée dès la pose aux personnages seulement");
        var newcomer = Unit("a"); newcomer.Zone = GameCardZone.Hand; auraState.PlayerOne.Hand.Add(newcomer);
        await engine.PlayCardAsync(auraState,"a",newcomer.InstanceId,3);
        check(newcomer.MaxHealth == 120 && auraState.PlayerOne.Characters[0]!.MaxHealth == 120,
            "audit : nouvelle carte reçoit l'aura sans cumuler le bonus sur les anciennes");
        auraState.ActivePlayerId = "b";
        auraState.PlayerTwo.Leader.Abilities.Add(new() { Id = 9, Type = AbilityType.Skill, Effects = new() {
            new() { Type = GameEffectType.Damage, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.EnemyCharacter, Value = 500 } } });
        await engine.UseAbilityAsync(auraState,"b",auraState.PlayerTwo.Leader.InstanceId,9,aura.InstanceId);
        check(auraState.PlayerOne.Characters[2] == null && newcomer.MaxHealth == 100 && auraState.PlayerOne.Characters[0]!.MaxHealth == 100,
            "audit : mort du fournisseur retire l'aura de PV des autres personnages");
        var regionState = State(); regionState.PlayerOne.Characters[0]!.RegionId = 1; regionState.PlayerOne.Characters[1]!.RegionId = 2;
        var regionProvider = Unit("a"); regionProvider.RegionId = 2; regionProvider.Zone = GameCardZone.Hand;
        regionProvider.Abilities.Add(new() { Type = AbilityType.Effect, Conditions = new() {
            new() { Type = GameConditionType.TargetRegion, Parameters = new() { ["regions"] = "1" } },
            new() { Type = GameConditionType.TargetHealthBelow, Value = 60 } }, Effects = new() {
            new() { Type = GameEffectType.HealthBonus, Trigger = GameEffectTrigger.OnPlay, Target = GameTargetType.AllFriendlyCharacters, Value = 20 } } });
        regionState.PlayerOne.Hand.Add(regionProvider); await engine.PlayCardAsync(regionState,"a",regionProvider.InstanceId,2);
        check(regionState.PlayerOne.Characters[0]!.MaxHealth == 120 && regionState.PlayerOne.Characters[1]!.MaxHealth == 100 && regionProvider.MaxHealth == 100,
            "audit : +20 PV à la pose seulement aux bénéficiaires de la région choisie");
        var healContext = new GameEffectContext { State = regionState, Player = regionState.PlayerOne, Source = regionProvider, Target = regionProvider };
        regionProvider.CurrentHealth = 95;
        await new HealEffectHandler().ExecuteAsync(healContext,new() { Value = 30 });
        check(regionProvider.CurrentHealth == 100 && healContext.Logs.Single().Contains("5 point"),
            "audit : soin plafonné annonce les PV réellement restaurés");
    }
}
