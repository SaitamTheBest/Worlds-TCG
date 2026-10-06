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

            var before = target.CurrentHealth.Value;
            target.CurrentHealth =
                Math.Min(
                    target.MaxHealth.Value,
                    target.CurrentHealth.Value +
                    (int)effect.Value);

            var restored = target.CurrentHealth.Value - before;
            context.Logs.Add(restored > 0
                ? $"{target.Name} récupère {restored} point(s) de vie ({target.CurrentHealth}/{target.MaxHealth})."
                : $"{target.Name} ne récupère aucun PV : ses PV sont déjà au maximum ou le soin vaut 0.");

            return Task.CompletedTask;
        }
    }
}
