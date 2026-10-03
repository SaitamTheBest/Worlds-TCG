using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects
{
    public sealed class HealEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.Heal;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var target = context.Target
                ?? throw new InvalidOperationException(
                    "Une cible est nécessaire.");

            if (!target.CurrentHealth.HasValue ||
                !target.MaxHealth.HasValue)
            {
                return Task.CompletedTask;
            }

            target.CurrentHealth =
                Math.Min(
                    target.MaxHealth.Value,
                    target.CurrentHealth.Value +
                    (int)effect.Value);

            context.Logs.Add(
                $"{target.Name} récupère {(int)effect.Value} point(s) de vie.");

            return Task.CompletedTask;
        }
    }
}
