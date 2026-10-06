using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Engine
{
    public sealed class GameTargetResolver
    {
        public IReadOnlyList<GameCardState> ResolveCards(
            GameState state,
            GamePlayerState player,
            GameCardState source,
            GameEffectDefinition effect,
            Guid? requestedTargetId)
        {
            var opponent = state.GetOpponent(player.UserId);

            IReadOnlyList<GameCardState> targets = effect.Target switch
            {
                GameTargetType.None => Array.Empty<GameCardState>(),

                GameTargetType.Self =>
                    new[] { source },

                GameTargetType.FriendlyLeader =>
                    new[] { player.Leader },

                GameTargetType.EnemyLeader =>
                    ResolveSpecific(
                        state,
                        player,
                        opponent.Leader,
                        effect,
                        requestedTargetId),

                GameTargetType.FriendlyCharacter =>
                    ResolveCharacter(
                        state,
                        player,
                        player,
                        effect,
                        requestedTargetId),

                GameTargetType.EnemyCharacter =>
                    ResolveCharacter(
                        state,
                        player,
                        opponent,
                        effect,
                        requestedTargetId),

                GameTargetType.FriendlyCharacterOrLeader =>
                    ResolveFriendlyUnit(
                        state,
                        player,
                        effect,
                        requestedTargetId),

                GameTargetType.EnemyCharacterOrLeader =>
                    ResolveEnemyUnit(
                        state,
                        player,
                        effect,
                        requestedTargetId),

                GameTargetType.FriendlyLocation =>
                    player.Location == null
                        ? Array.Empty<GameCardState>()
                        : new[] { player.Location },

                GameTargetType.EnemyLocation =>
                    opponent.Location == null
                        ? Array.Empty<GameCardState>()
                        : new[] { opponent.Location },

                GameTargetType.AllFriendlyCharacters =>
                    player.Characters
                        .Where(x => x != null && (x.CurrentHealth == null || x.CurrentHealth > 0))
                        .Cast<GameCardState>()
                        .ToList(),

                GameTargetType.AllEnemyCharacters =>
                    opponent.Characters
                        .Where(x => x != null && (x.CurrentHealth == null || x.CurrentHealth > 0))
                        .Cast<GameCardState>()
                        .ToList(),
                GameTargetType.AllCharacters => GameGeography.Characters(state,player,3).ToList(),

                _ => Array.Empty<GameCardState>()
            };
            var filtered = targets.Where(c => GameGeography.Matches(c,effect.Parameters)).ToList();
            if (targets.Count > 0 && filtered.Count == 0 && effect.Target is GameTargetType.FriendlyCharacter or GameTargetType.FriendlyCharacterOrLeader or GameTargetType.EnemyCharacter or GameTargetType.EnemyLeader or GameTargetType.EnemyCharacterOrLeader)
                throw new GameRuleException("Cette cible ne respecte pas le filtre de région, de pays ou de parti politique.");
            return filtered;
        }

        public GamePlayerState ResolvePlayer(
            GameState state,
            GamePlayerState player,
            GameTargetType target)
        {
            return target switch
            {
                GameTargetType.EnemyPlayer =>
                    state.GetOpponent(player.UserId),

                _ => player
            };
        }

        private IReadOnlyList<GameCardState> ResolveCharacter(
            GameState state,
            GamePlayerState player,
            GamePlayerState owner,
            GameEffectDefinition effect,
            Guid? requestedTargetId)
        {
            if (!requestedTargetId.HasValue)
                throw new GameRuleException("Une cible est nécessaire.");

            var card = owner.Characters
                .FirstOrDefault(x =>
                    x?.InstanceId == requestedTargetId.Value);

            if (card == null)
            {
                if (effect.Trigger == GameEffectTrigger.OnDamaged)
                    return Array.Empty<GameCardState>();
                throw new GameRuleException("Personnage cible introuvable.");
            }

            return ResolveSpecific(
                state,
                player,
                card,
                effect,
                requestedTargetId);
        }

        private IReadOnlyList<GameCardState> ResolveFriendlyUnit(
            GameState state,
            GamePlayerState player,
            GameEffectDefinition effect,
            Guid? requestedTargetId)
        {
            if (!requestedTargetId.HasValue)
                throw new GameRuleException("Une cible est nécessaire.");

            var target = state.FindCard(requestedTargetId.Value);

            if (target == null ||
                target.ControllerUserId != player.UserId ||
                target.Zone is not (GameCardZone.Leader or GameCardZone.CharacterBoard))
            {
                throw new GameRuleException("Cible alliée invalide.");
            }

            return new[] { target };
        }

        private IReadOnlyList<GameCardState> ResolveEnemyUnit(
            GameState state,
            GamePlayerState player,
            GameEffectDefinition effect,
            Guid? requestedTargetId)
        {
            if (!requestedTargetId.HasValue)
                throw new GameRuleException("Une cible est nécessaire.");

            var opponent = state.GetOpponent(player.UserId);
            var target = state.FindCard(requestedTargetId.Value);

            if (target == null ||
                target.ControllerUserId != opponent.UserId ||
                target.Zone is not (GameCardZone.Leader or GameCardZone.CharacterBoard))
            {
                throw new GameRuleException("Cible adverse invalide.");
            }

            return ResolveSpecific(
                state,
                player,
                target,
                effect,
                requestedTargetId);
        }

        private IReadOnlyList<GameCardState> ResolveSpecific(
            GameState state,
            GamePlayerState player,
            GameCardState target,
            GameEffectDefinition effect,
            Guid? requestedTargetId)
        {
            if (target.ControllerUserId != player.UserId && target.IsStealthed)
            {
                if (IsAutomatic(effect.Trigger)) return Array.Empty<GameCardState>();
                throw new GameRuleException("Cette carte est camouflée : elle ne peut pas être ciblée par l'adversaire.");
            }

            if (effect.Type == GameEffectType.Damage && target.ControllerUserId != player.UserId)
            {
                var opponent = state.GetOpponent(player.UserId);

                var blockers = opponent.Characters
                    .Where(x =>
                        x != null &&
                        x.IsBlocking &&
                        x.BlockExpiresAfterTurn >= state.TurnNumber)
                    .Cast<GameCardState>()
                    .ToList();

                if (blockers.Count > 0 &&
                    blockers.All(x => x.InstanceId != target.InstanceId))
                {
                    if (IsAutomatic(effect.Trigger)) return Array.Empty<GameCardState>();
                    throw new GameRuleException(
                        "Une carte adverse bloque les attaques.");
                }
            }

            return new[] { target };
        }

        private static bool IsAutomatic(GameEffectTrigger trigger) => trigger is GameEffectTrigger.Passive
            or GameEffectTrigger.OnDeath or GameEffectTrigger.OnTurnStart or GameEffectTrigger.OnTurnEnd or GameEffectTrigger.OnDamaged;
    }
}
