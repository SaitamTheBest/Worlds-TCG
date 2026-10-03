using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects
{
    public sealed class GameEffectEngine
    {
        private readonly Dictionary<
            GameEffectType,
            IGameEffectHandler> _handlers;

        private readonly GameTargetResolver _targetResolver;

        public GameEffectEngine(
            IEnumerable<IGameEffectHandler> handlers,
            GameTargetResolver targetResolver)
        {
            _handlers = handlers.ToDictionary(
                x => x.EffectType);

            _targetResolver = targetResolver;
        }

        public async Task ExecuteAsync(
            GameEffectContext context,
            IEnumerable<GameEffectDefinition> effects,
            GameEffectTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            foreach (var effect in effects.Where(
                         x => x.Trigger == trigger))
            {
                if (!_handlers.TryGetValue(
                        effect.Type,
                        out var handler))
                {
                    throw new InvalidOperationException(
                        $"Aucun handler enregistré pour {effect.Type}.");
                }

                var targets =
                    _targetResolver.ResolveCards(
                        context.State,
                        context.Player,
                        context.Source ?? context.Player.Leader,
                        effect,
                        context.RequestedTargetId);

                if (targets.Count == 0)
                {
                    if (effect.Target is not (GameTargetType.None or GameTargetType.FriendlyPlayer or GameTargetType.EnemyPlayer))
                        continue;
                    context.Target = null;

                    await handler.ExecuteAsync(
                        context,
                        effect,
                        cancellationToken);

                    continue;
                }

                foreach (var target in targets)
                {
                    if (!new GameConditionEvaluator().AreMet(context.State,context.Player,context.Source ?? context.Player.Leader,target,context.TargetConditions)) continue;
                    context.Target = target;
                    var healthBefore = target.CurrentHealth;

                    await handler.ExecuteAsync(
                        context,
                        effect,
                        cancellationToken);

                    if (effect.Type == GameEffectType.Damage && target.CurrentHealth < healthBefore)
                    {
                        var owner = context.State.GetPlayer(target.OwnerUserId);
                        foreach (var ability in target.Abilities)
                        {
                            if (!new GameConditionEvaluator().AreMet(context.State, owner, target, context.Source, ability.Conditions)) continue;
                            var reaction = new GameEffectContext { State = context.State, Player = owner, Source = target, RequestedTargetId = context.Source?.InstanceId,
                                TargetConditions = ability.Conditions.Where(GameConditionEvaluator.IsTargetGeography).ToList() };
                            await ExecuteAsync(reaction, ability.Effects, GameEffectTrigger.OnDamaged, cancellationToken);
                            context.Logs.AddRange(reaction.Logs);
                        }
                    }
                }
            }
        }

        public void ValidateTargets(GameEffectContext context, IEnumerable<GameEffectDefinition> effects, GameEffectTrigger trigger)
        {
            foreach (var effect in effects.Where(e => e.Trigger == trigger))
                _targetResolver.ResolveCards(context.State, context.Player, context.Source ?? context.Player.Leader, effect, context.RequestedTargetId);
        }
    }
}
