using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects
{
    public sealed class GainMoneyEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.GainMoney;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var player =
                effect.Target == GameTargetType.EnemyPlayer ? context.Opponent : context.Player;

            var gained = web_tcg.Game.Engine.GameEnergy.Gain(player, (int)effect.Value);

            context.Logs.Add(
                $"{player.DisplayName} gagne {gained} énergie.");

            return Task.CompletedTask;
        }
    }

    public sealed class LoseMoneyEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.LoseMoney;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var opponent =
                effect.Target == GameTargetType.FriendlyPlayer ? context.Player : context.Opponent;

            var before = opponent.Money;
            opponent.Money =
                Math.Max(
                    0,
                    opponent.Money - (int)effect.Value);

            context.Logs.Add(
                $"{opponent.DisplayName} perd {before - opponent.Money} énergie.");

            return Task.CompletedTask;
        }
    }

    public sealed class StealMoneyEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.StealMoney;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var player =
                context.Player;

            var opponent =
                context.Opponent;

            var amount =
                Math.Min(
                    (int)effect.Value,
                    opponent.Money);

            // Transfer only the amount that can actually be stored by the recipient.
            amount = web_tcg.Game.Engine.GameEnergy.Gain(player, amount);

            opponent.Money -=
                amount;

            context.Logs.Add(
                $"{player.DisplayName} vole {amount} énergie à {opponent.DisplayName}.");

            return Task.CompletedTask;
        }
    }
}
