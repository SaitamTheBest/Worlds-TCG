using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects
{
    public sealed class DrawCardsEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.DrawCards;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var player =
                effect.Target == GameTargetType.EnemyPlayer ? context.Opponent : context.Player;

            var count =
                (int)effect.Value;

            var drawn = 0;

            for (var i = 0;
                 i < count && player.Deck.Count > 0;
                 i++)
            {
                var card =
                    player.Deck[0];

                player.Deck.RemoveAt(0);

                card.Zone =
                    GameCardZone.Hand;

                player.Hand.Add(
                    card);

                drawn++;
            }

            if (drawn > 0)
            {
                context.Logs.Add(
                    $"{player.DisplayName} pioche {drawn} carte(s).");
            }

            return Task.CompletedTask;
        }
    }
}
