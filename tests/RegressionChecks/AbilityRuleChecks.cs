using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Effects;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;
using web_tcg.Models.Admin;
using web_tcg.Services;

public static class AbilityRuleChecks
{
    public static async Task RunAsync(Action<bool,string> check, ApplicationDbContext db)
    {
        var input = new CardAbilityInputModel { Type = AbilityType.Trait, Name = "Talent", Description = "Soin", CooldownTurns = 2,
            Effects = new() { new() { Type = GameEffectType.Heal, Trigger = GameEffectTrigger.OnTalent, Target = GameTargetType.Self, Value = 25 } },
            Conditions = new() { new() { Type = GameConditionType.SourceHealthBelow, Value = 100 } } };
        var stored = new CardAbility { Id = 800, CardId = 1, Type = input.Type, Name = input.Name, Description = input.Description,
            Effects = new List<CardAbilityEffect> { new() { Id = 801, Type = GameEffectType.Damage } } };
        db.ChangeTracker.Clear(); db.Attach(stored); var removed = stored.Effects.Single();
        CardAbilityEditor.Write(stored,input); db.ChangeTracker.DetectChanges();
        var loaded = CardAbilityEditor.Read(stored);
        check(loaded.CooldownTurns == 2 && loaded.Effects.Single().Value == 25 && loaded.Conditions.Single().Value == 100,
            "éditeur : effets, recharge et conditions conservés en modification");
        check(db.Entry(removed).State == EntityState.Deleted && db.Entry(stored.Effects.Single()).State == EntityState.Added,
            "éditeur : remplacement des actions suivi comme suppression et insertion EF");
        var freeEffect = new CardAbility { UsageCost = 30 };
        CardAbilityEditor.Write(freeEffect,new CardAbilityInputModel { Type = AbilityType.Effect, UsageCost = 30 });
        check(freeEffect.UsageCost == 0,"éditeur : un effet ne conserve jamais un coût même si une valeur est envoyée");
        check(!CardAbilityEditor.Validate(CardType.Character,input).Any(), "éditeur : talent de soin valide");
        var forbidden = new CardAbilityInputModel { Type = AbilityType.Effect, Effects = new() { new() { Type = GameEffectType.Block, Trigger = GameEffectTrigger.Passive, Target = GameTargetType.Self } } };
        check(CardAbilityEditor.Validate(CardType.Leader,forbidden).Any() && CardAbilityEditor.Validate(CardType.Location,forbidden).Any(),
            "éditeur : refus du blocage du Leader et des états donnés par un lieu");
        var effects = new GameEffectEngine(new IGameEffectHandler[] { new DamageEffectHandler(), new HealEffectHandler(), new ShieldEffectHandler(), new StealthEffectHandler(),
            new DamageMultiplierEffectHandler(), new ExtraAttackEffectHandler(), new BlockEffectHandler(), new PrisonEffectHandler(), new ReviveEffectHandler(), new GainMoneyEffectHandler(), new DestroyLocationEffectHandler() }, new GameTargetResolver());
        var engine = new GameEngine(effects,new GameConditionEvaluator());
        GameCardState Unit(CardType type,string owner) => new() { Type = type, OwnerUserId = owner, ControllerUserId = owner, CurrentHealth = 100, MaxHealth = 100,
            Zone = type == CardType.Leader ? GameCardZone.Leader : GameCardZone.CharacterBoard };
        var state = new GameState { Status = GameMatchStatus.InProgress, ActivePlayerId = "a", TurnNumber = 20,
            PlayerOne = new() { UserId = "a", TurnsPlayed = 3, Money = 50, Leader = Unit(CardType.Leader,"a") },
            PlayerTwo = new() { UserId = "b", TurnsPlayed = 2, Money = 50, Leader = Unit(CardType.Leader,"b") } };
        async Task<bool> Rejected(Func<Task> action) { try { await action(); return false; } catch (GameRuleException) { return true; } }
        check(await Rejected(()=>engine.BlockAsync(state,"a",state.PlayerOne.Leader.InstanceId)) && !state.PlayerOne.Leader.IsBlocking,
            "moteur : Leader incapable de bloquer sans changer son état");
        var character = Unit(CardType.Character,"a"); state.PlayerOne.Characters[0] = character;
        character.Abilities.Add(new() { Id = 1, Type = AbilityType.Trait, CooldownTurns = 2, UsageCost = 5, Effects = new() {
            new() { Type = GameEffectType.Heal, Trigger = GameEffectTrigger.OnTalent, Target = GameTargetType.Self, Value = 20 } } });
        character.IsExhausted = true;
        check(await Rejected(()=>engine.UseAbilityAsync(state,"a",character.InstanceId,1,null)) && state.PlayerOne.Money == 50,
            "moteur : talent interdit pendant l'épuisement, énergie conservée");
        character.IsExhausted = false; character.CurrentHealth = 60;
        await engine.UseAbilityAsync(state,"a",character.InstanceId,1,null);
        check(!character.IsExhausted && character.CurrentHealth == 80 && character.AbilityCooldowns[1] == 2,
            "moteur : talent soigne sans épuiser et démarre sa recharge");
        var context = new GameEffectContext { State = state, Player = state.PlayerOne, Source = character };
        await effects.ExecuteAsync(context,new[] { new GameEffectDefinition { Type = GameEffectType.DamageMultiplier, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.Self, Value = 2, DurationTurns = 1 },
            new GameEffectDefinition { Type = GameEffectType.ExtraAttack, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.Self, Value = 1, DurationTurns = 1 } },GameEffectTrigger.OnSkill);
        check(character.DamageMultiplier == 2 && character.ExtraSkillUses == 1 && character.AppliedEffects.All(e=>e.ExpiresOnOwnerTurn == 5),
            "moteur : bonus immédiats et durée basée sur les prochains tours du propriétaire");
        character.Abilities.Add(new() { Id = 2, Type = AbilityType.Skill, Effects = new() { new() { Type = GameEffectType.Damage, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.EnemyLeader, Value = 10 } } });
        await engine.UseAbilityAsync(state,"a",character.InstanceId,2,state.PlayerTwo.Leader.InstanceId);
        check(state.PlayerTwo.Leader.CurrentHealth == 80 && !character.IsExhausted, "moteur : dégâts doublés et première attaque supplémentaire disponible");
        await engine.UseAbilityAsync(state,"a",character.InstanceId,2,state.PlayerTwo.Leader.InstanceId);
        check(state.PlayerTwo.Leader.CurrentHealth == 60 && character.IsExhausted, "moteur : deuxième attaque puis épuisement");
        var decision = Unit(CardType.PoliticalDecision,"a"); decision.Zone = GameCardZone.Hand; decision.UsageCost = 10;
        decision.Abilities.Add(new() { Type = AbilityType.Effect, Effects = new() { new() { Type = GameEffectType.Heal, Trigger = GameEffectTrigger.OnPlay, Target = GameTargetType.FriendlyCharacter, Value = 10 } } });
        state.PlayerOne.Hand.Add(decision); var before = state.PlayerOne.Money;
        check(await Rejected(()=>engine.PlayCardAsync(state,"a",decision.InstanceId,null)) && state.PlayerOne.Hand.Contains(decision) && state.PlayerOne.Money == before,
            "moteur : cible manquante refusée avant de dépenser ou défausser la décision");
        await engine.PlayCardAsync(state,"a",decision.InstanceId,null,targetInstanceId:character.InstanceId);
        check(character.CurrentHealth == 90 && state.PlayerOne.Graveyard.Contains(decision), "moteur : décision soigne sa cible puis rejoint le cimetière");
        var revived = Unit(CardType.Character,"b"); state.PlayerTwo.Characters[0] = revived;
        revived.Abilities.Add(new() { Type = AbilityType.Effect, Effects = new() { new() { Type = GameEffectType.Revive, Trigger = GameEffectTrigger.OnDeath, Target = GameTargetType.Self } } });
        character.IsExhausted = false; character.ExtraSkillUses = 0;
        character.Abilities.Add(new() { Id = 3, Type = AbilityType.Skill, Effects = new() { new() { Type = GameEffectType.Damage, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.EnemyCharacter, Value = 100 } } });
        await engine.UseAbilityAsync(state,"a",character.InstanceId,3,revived.InstanceId);
        check(revived.CurrentHealth == 50 && revived.IsExhausted && state.PlayerTwo.Characters[0] == revived && revived.HasRevived,
            "moteur : Second souffle ressuscite à 50 % dans l'emplacement occupé");
        character.IsExhausted = false;
        await engine.UseAbilityAsync(state,"a",character.InstanceId,3,revived.InstanceId);
        check(state.PlayerTwo.Characters[0] == null && state.PlayerTwo.Graveyard.Contains(revived), "moteur : Second souffle ne ressuscite pas indéfiniment");
        var passive = Unit(CardType.Character,"a"); passive.Zone = GameCardZone.Hand; passive.Abilities.Add(new() { Type = AbilityType.Effect,
            Effects = new() { new() { Type = GameEffectType.Shield, Trigger = GameEffectTrigger.Passive, Target = GameTargetType.Self, Value = 1 } } });
        state.PlayerOne.Hand.Add(passive); await engine.PlayCardAsync(state,"a",passive.InstanceId,1);
        check(passive.HasShield && passive.IsExhausted, "moteur : bouclier passif appliqué dès la pose malgré l'épuisement");
        var location = Unit(CardType.Location,"a"); location.Zone = GameCardZone.Hand; location.Abilities.Add(new() { Type = AbilityType.Effect,
            Effects = new() { new() { Type = GameEffectType.DamageMultiplier, Trigger = GameEffectTrigger.Passive, Target = GameTargetType.AllFriendlyCharacters, Value = 3 } } });
        state.PlayerOne.Hand.Add(location); await engine.PlayCardAsync(state,"a",location.InstanceId,null);
        check(passive.DamageMultiplier == 3, "moteur : bonus de groupe du lieu appliqué aux personnages présents");
        var newcomer = Unit(CardType.Character,"a"); newcomer.Zone = GameCardZone.Hand; state.PlayerOne.Hand.Add(newcomer);
        await engine.PlayCardAsync(state,"a",newcomer.InstanceId,2);
        check(newcomer.DamageMultiplier == 3, "moteur : bonus passif du lieu appliqué aussi à un nouveau personnage");
        var destruction = Unit(CardType.PoliticalDecision,"b"); destruction.Zone = GameCardZone.Hand;
        destruction.Abilities.Add(new() { Type = AbilityType.Effect, Effects = new() { new() { Type = GameEffectType.DestroyLocation, Trigger = GameEffectTrigger.OnPlay, Target = GameTargetType.EnemyLocation } } });
        state.PlayerTwo.Hand.Add(destruction); state.ActivePlayerId = "b";
        await engine.PlayCardAsync(state,"b",destruction.InstanceId,null);
        check(state.PlayerOne.Location == null && passive.DamageMultiplier == 1 && newcomer.DamageMultiplier == 1,
            "moteur : destruction du lieu retire les bonus liés à sa présence");
        state.ActivePlayerId = "a";
        var conditional = Unit(CardType.PoliticalDecision,"a"); conditional.Zone = GameCardZone.Hand; conditional.UsageCost = 10;
        conditional.Abilities.Add(new() { Type = AbilityType.Effect, Conditions = new() { new() { Type = GameConditionType.PlayerMoneyAtLeast, Value = 10 } },
            Effects = new() { new() { Type = GameEffectType.GainMoney, Trigger = GameEffectTrigger.OnPlay, Target = GameTargetType.FriendlyPlayer, Value = 5 } } });
        state.PlayerOne.Hand.Add(conditional); state.PlayerOne.Money = 9;
        check(await Rejected(()=>engine.PlayCardAsync(state,"a",conditional.InstanceId,null)) && state.PlayerOne.Hand.Contains(conditional),
            "moteur : décision refusée si ses conditions de pose ne sont pas remplies");
        state.PlayerOne.Money = 10; await engine.PlayCardAsync(state,"a",conditional.InstanceId,null);
        check(state.PlayerOne.Money == 5, "moteur : conditions de pose évaluées avant le paiement, action exécutée ensuite");

        var prisonInput = new CardAbilityInputModel { Type = AbilityType.Skill, Effects = new() {
            new() { Type = GameEffectType.Prison, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.EnemyCharacter, DurationTurns = 1 } } };
        check(!CardAbilityEditor.Validate(CardType.Character, prisonInput).Any(), "éditeur : action Prison ciblée valide pour un tour");
        prisonInput.Effects[0].DurationTurns = 2;
        check(CardAbilityEditor.Validate(CardType.Character, prisonInput).Any(), "éditeur : Prison ne peut pas durer plusieurs tours");
        prisonInput.Type = AbilityType.Effect; prisonInput.Effects[0].Trigger = GameEffectTrigger.Passive; prisonInput.Effects[0].DurationTurns = 0;
        check(CardAbilityEditor.Validate(CardType.Character, prisonInput).Any() && CardAbilityEditor.Validate(CardType.Location, prisonInput).Any(),
            "éditeur : Prison passive et état donné par un lieu interdits");

        var prisonState = new GameState { Status = GameMatchStatus.InProgress, ActivePlayerId = "a", TurnNumber = 6,
            PlayerOne = new() { UserId = "a", TurnsPlayed = 3, Money = 50, Leader = Unit(CardType.Leader,"a") },
            PlayerTwo = new() { UserId = "b", TurnsPlayed = 3, Money = 50, Leader = Unit(CardType.Leader,"b") } };
        var prisoner = Unit(CardType.Character,"b"); prisoner.Name = "Prisonnier"; prisonState.PlayerTwo.Characters[0] = prisoner;
        prisoner.Abilities.Add(new() { Id = 10, Type = AbilityType.Skill, UsageCost = 5, Effects = new() {
            new() { Type = GameEffectType.Damage, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.EnemyLeader, Value = 10 } } });
        prisoner.Abilities.Add(new() { Id = 11, Type = AbilityType.Trait, UsageCost = 5, Effects = new() {
            new() { Type = GameEffectType.Heal, Trigger = GameEffectTrigger.OnTalent, Target = GameTargetType.Self, Value = 10 } } });
        prisonState.PlayerOne.Leader.Abilities.Add(new() { Id = 12, Type = AbilityType.Skill, UsageCost = 5, Effects = new() {
            new() { Type = GameEffectType.Prison, Trigger = GameEffectTrigger.OnSkill, Target = GameTargetType.EnemyCharacter, DurationTurns = 99 } } });
        await engine.UseAbilityAsync(prisonState,"a",prisonState.PlayerOne.Leader.InstanceId,12,prisoner.InstanceId);
        var restored = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(prisonState))!;
        check(prisoner.IsImprisoned && restored.PlayerTwo.Characters[0]!.IsImprisoned && prisonState.PlayerOne.Money == 45,
            "moteur : compétence Prison applique et sauvegarde l'état, durée runtime limitée à un tour");
        await engine.EndTurnAsync(prisonState,"a");
        var prisonMoney = prisonState.PlayerTwo.Money;
        check(prisoner.IsImprisoned && !prisoner.IsExhausted &&
            await Rejected(()=>engine.UseAbilityAsync(prisonState,"b",prisoner.InstanceId,10,prisonState.PlayerOne.Leader.InstanceId)) &&
            await Rejected(()=>engine.UseAbilityAsync(prisonState,"b",prisoner.InstanceId,11,null)) &&
            await Rejected(()=>engine.BlockAsync(prisonState,"b",prisoner.InstanceId)) && prisonState.PlayerTwo.Money == prisonMoney,
            "moteur : Prison bloque compétence, talent et blocage au prochain tour sans dépenser d'énergie");
        check(new GameStateProjector().Project(prisonState,"b").Me.Characters[0]!.IsImprisoned,
            "Play : l'état Prison est transmis au propriétaire");
        var releaseLogs = await engine.EndTurnAsync(prisonState,"b");
        check(!prisoner.IsImprisoned && releaseLogs.Any(l=>l.Contains("sort de prison")), "moteur : libération dès la fin du prochain tour du propriétaire");
        await engine.EndTurnAsync(prisonState,"a");
        await engine.UseAbilityAsync(prisonState,"b",prisoner.InstanceId,10,prisonState.PlayerOne.Leader.InstanceId);
        check(prisonState.PlayerOne.Leader.CurrentHealth == 90, "moteur : personnage libéré peut attaquer au tour suivant");

        var jail = new PrisonEffectHandler();
        var jailContext = new GameEffectContext { State = prisonState, Player = prisonState.PlayerTwo, Source = prisoner, Target = prisonState.PlayerTwo.Leader };
        var jailDefinition = new GameEffectDefinition { Type = GameEffectType.Prison, Trigger = GameEffectTrigger.OnTalent, DurationTurns = 1 };
        await jail.ExecuteAsync(jailContext, jailDefinition); await jail.ExecuteAsync(jailContext, jailDefinition);
        check(prisonState.PlayerTwo.Leader.IsImprisoned && prisonState.PlayerTwo.Leader.AppliedEffects.Count(e=>e.Type == GameEffectType.Prison) == 1,
            "moteur : Leader ciblable et répétition de Prison sans cumul de durée");
        await engine.EndTurnAsync(prisonState,"b");
        check(prisonState.PlayerTwo.Leader.IsImprisoned, "moteur : Prison appliquée pendant son propre tour ne disparaît pas à la fin de ce tour");
        await engine.EndTurnAsync(prisonState,"a"); await engine.EndTurnAsync(prisonState,"b");
        check(!prisonState.PlayerTwo.Leader.IsImprisoned, "moteur : Prison sur son propre tour dure jusqu'à la fin du prochain tour");
    }
}
