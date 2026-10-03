using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects
{
    public sealed class DamageEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.Damage;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var target = context.Target
                ?? throw new InvalidOperationException(
                    "Une cible est nécessaire.");

            if (!target.CurrentHealth.HasValue)
            {
                throw new InvalidOperationException(
                    "Cette carte ne possède pas de points de vie.");
            }

            /*
             * Bouclier :
             * protège complètement de la première attaque.
             */
            if (target.HasShield)
            {
                target.HasShield = false;

                return Task.CompletedTask;
            }

            target.CurrentHealth =
                Math.Max(
                    0,
                    target.CurrentHealth.Value -
                    (int)((effect.Value + (context.Source?.DamageBonus ?? 0)) * (context.Source?.DamageMultiplier ?? 1m)));

            return Task.CompletedTask;
        }
    }
}
