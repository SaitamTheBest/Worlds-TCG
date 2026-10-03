using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects
{
    public sealed class ReviveEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.Revive;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var target = context.Target
                ?? throw new InvalidOperationException(
                    "Une cible est nécessaire.");

            if (!target.MaxHealth.HasValue)
                return Task.CompletedTask;

            if (target.Type != CardType.Character || target.HasRevived)
                return Task.CompletedTask;

            var player =
                context.State.GetPlayer(
                    target.OwnerUserId);

            // OnDeath is executed before the card leaves its occupied slot.
            var dyingOnBoard = target.Zone == GameCardZone.CharacterBoard && target.CurrentHealth <= 0;
            if (!dyingOnBoard && !player.Graveyard.Remove(target))
                return Task.CompletedTask;

            target.CurrentHealth =
                Math.Max(
                    1,
                    (target.BaseHealth ?? target.MaxHealth.Value) / 2);

            target.HasShield = false;
            target.IsStealthed = false;
            target.IsBlocking = false;
            target.IsExhausted = true;
            target.AppliedEffects.Clear();
            target.ExtraSkillUses = 0;
            target.DamageMultiplier = 1m;
            target.DamageBonus = 0;
            target.MaxHealth = target.BaseHealth ?? target.MaxHealth;
            target.BlockExpiresAfterTurn = null;
            target.HasRevived = true;
            target.ReadyOnOwnerTurn = player.TurnsPlayed + 1;

            if (dyingOnBoard)
            {
                context.Logs.Add($"{target.Name} revient à la vie avec la moitié de ses PV.");
                return Task.CompletedTask;
            }

            var freeSlot =
                player.Characters
                    .Select((card, index) => (card, index))
                    .Where(x => x.card == null)
                    .Select(x => x.index)
                    .Cast<int?>()
                    .FirstOrDefault();

            if (freeSlot == null)
            {
                player.Graveyard.Add(
                    target);

                return Task.CompletedTask;
            }

            target.Zone =
                GameCardZone.CharacterBoard;

            target.BoardSlot =
                freeSlot;

            target.ReadyOnOwnerTurn =
                player.TurnsPlayed + 1;

            player.Characters[freeSlot.Value] =
                target;

            context.Logs.Add(
                $"{target.Name} revient à la vie.");

            return Task.CompletedTask;
        }
    }

    public sealed class DestroyLocationEffectHandler :
        IGameEffectHandler
    {
        public GameEffectType EffectType =>
            GameEffectType.DestroyLocation;

        public Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default)
        {
            var opponent =
                context.Opponent;

            if (opponent.Location == null)
                return Task.CompletedTask;

            var location =
                opponent.Location;

            location.Zone =
                GameCardZone.Graveyard;

            opponent.Graveyard.Add(
                location);

            opponent.Location =
                null;

            context.Logs.Add(
                $"{location.Name} est détruit.");

            return Task.CompletedTask;
        }
    }
}
