using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects
{
    public sealed class PrisonEffectHandler : IGameEffectHandler
    {
        public GameEffectType EffectType => GameEffectType.Prison;

        public Task ExecuteAsync(GameEffectContext context, GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var target = context.Target ?? throw new InvalidOperationException("Une cible est nécessaire.");
            if (target.Type is not (CardType.Character or CardType.Leader))
                throw new web_tcg.Game.Engine.GameRuleException("Prison cible un personnage ou un Leader.");
            if (effect.Trigger == GameEffectTrigger.Passive || context.Source?.Type == CardType.Location)
                throw new web_tcg.Game.Engine.GameRuleException("Prison ne peut pas être un effet passif ou un effet de lieu.");

            // Fixed duration, independent of submitted values. Reapplying refreshes, never stacks turns.
            target.AppliedEffects.RemoveAll(e => e.Type == GameEffectType.Prison);
            target.AppliedEffects.Add(new GameAppliedEffect
            {
                Type = GameEffectType.Prison,
                Value = 1,
                ExpiresOnOwnerTurn = context.State.GetPlayer(target.OwnerUserId).TurnsPlayed + 2
            });
            context.Logs.Add($"{target.Name} est en prison : aucune action jusqu'à la fin de son prochain tour.");
            return Task.CompletedTask;
        }
    }

    public sealed class ShieldEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.Shield;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var target = context.Target
                ?? throw new InvalidOperationException(
                    "Une cible est nécessaire.");

            if (!ApplyDurableEffect(
                target,
                effect,
                context)) return Task.CompletedTask;
            target.HasShield = true;

            context.Logs.Add(
                $"{target.Name} obtient un bouclier.");

            return Task.CompletedTask;
        }

        internal static bool ApplyDurableEffect(
            GameCardState target,
            GameEffectDefinition effect,
            GameEffectContext context,
            decimal? appliedValue = null)
        {
            var currentTurn = context.State.GetPlayer(target.OwnerUserId).TurnsPlayed;
            var sourceId = effect.Trigger == GameEffectTrigger.Passive || context.Source?.Type == CardType.Location ? context.Source?.InstanceId : null;
            var definitionKey = System.Text.Json.JsonSerializer.Serialize(effect);
            var existing = sourceId.HasValue ? target.AppliedEffects.FirstOrDefault(e => e.SourceInstanceId == sourceId && e.Type == effect.Type &&
                (e.DefinitionKey == definitionKey || e.DefinitionKey == null && e.Value == effect.Value)) : null;
            if (existing != null) { existing.Value = appliedValue ?? effect.Value; return false; }
            target.AppliedEffects.Add(
                new GameAppliedEffect
                {
                    Type = effect.Type,
                    Value = appliedValue ?? effect.Value,
                    DefinitionKey = definitionKey,
                    SourceInstanceId = sourceId,
                    ExpiresOnOwnerTurn =
                        effect.DurationTurns > 0
                            ? currentTurn + effect.DurationTurns + 1
                            : null
                });
            return true;
        }
    }

    public sealed class StealthEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.Stealth;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var target = context.Target
                ?? throw new InvalidOperationException(
                    "Une cible est nécessaire.");

            if (!ShieldEffectHandler.ApplyDurableEffect(
                target,
                effect,
                context)) return Task.CompletedTask;
            target.IsStealthed = true;

            context.Logs.Add(
                $"{target.Name} se camoufle.");

            return Task.CompletedTask;
        }
    }

    public sealed class ExtraAttackEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.ExtraAttack;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var target = context.Target
                ?? throw new InvalidOperationException(
                    "Une cible est nécessaire.");

            if (!ShieldEffectHandler.ApplyDurableEffect(
                target,
                effect,
                context)) return Task.CompletedTask;

            target.ExtraSkillUses = Math.Max(target.ExtraSkillUses, Math.Max(1, (int)effect.Value));

            context.Logs.Add(
                $"{target.Name} peut attaquer à nouveau.");

            return Task.CompletedTask;
        }
    }

    public sealed class DamageMultiplierEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.DamageMultiplier;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var target = context.Target
                ?? throw new InvalidOperationException(
                    "Une cible est nécessaire.");

            if (!ShieldEffectHandler.ApplyDurableEffect(
                target,
                effect,
                context)) return Task.CompletedTask;

            target.DamageMultiplier = Math.Max(target.DamageMultiplier, Math.Max(1m, effect.Value));

            context.Logs.Add(
                $"{target.Name} inflige des dégâts multipliés.");

            return Task.CompletedTask;
        }
    }

    public sealed class BlockEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.Block;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var target = context.Target
                ?? throw new InvalidOperationException(
                    "Une cible est nécessaire.");

            if (target.Type == CardType.Leader)
                throw new web_tcg.Game.Engine.GameRuleException("Un Leader ne peut pas bloquer une attaque.");
            if (effect.Trigger == GameEffectTrigger.Passive && !ShieldEffectHandler.ApplyDurableEffect(target, effect, context))
                return Task.CompletedTask;
            target.IsBlocking = true;
            target.IsExhausted = true;
            target.ReadyOnOwnerTurn = context.State.GetPlayer(target.OwnerUserId).TurnsPlayed + 1;

            target.BlockExpiresAfterTurn =
                effect.DurationTurns > 0 ? context.State.TurnNumber + effect.DurationTurns * 2 + 1 : int.MaxValue;

            context.Logs.Add(
                $"{target.Name} obtient Provocation.");

            return Task.CompletedTask;
        }
    }
}
