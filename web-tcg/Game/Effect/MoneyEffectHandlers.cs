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

            player.Money +=
                (int)effect.Value;

            context.Logs.Add(
                $"{player.DisplayName} gagne {(int)effect.Value} énergie.");

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

            opponent.Money =
                Math.Max(
                    0,
                    opponent.Money - (int)effect.Value);

            context.Logs.Add(
                $"{opponent.DisplayName} perd {(int)effect.Value} énergie.");

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

            opponent.Money -=
                amount;

            player.Money +=
                amount;

            context.Logs.Add(
                $"{player.DisplayName} vole {amount} énergie à {opponent.DisplayName}.");

            return Task.CompletedTask;
        }
    }
}
