using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects
{
    public sealed class DiscardCardsEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.DiscardCards;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var opponent =
                effect.Target == GameTargetType.FriendlyPlayer ? context.Player : context.Opponent;

            var count =
                (int)effect.Value;

            var discarded = 0;

            for (var i = 0;
                 i < count && opponent.Hand.Count > 0;
                 i++)
            {
                var index =
                    Random.Shared.Next(
                        opponent.Hand.Count);

                var card =
                    opponent.Hand[index];

                opponent.Hand.RemoveAt(index);

                card.Zone =
                    GameCardZone.Graveyard;

                opponent.Graveyard.Add(
                    card);

                discarded++;
            }

            if (discarded > 0)
            {
                context.Logs.Add(
                    $"{opponent.DisplayName} défausse {discarded} carte(s).");
            }

            return Task.CompletedTask;
        }
    }
}
