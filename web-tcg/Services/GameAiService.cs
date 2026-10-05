using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;

namespace web_tcg.Services
{
    /// <summary>
    /// Intelligence artificielle basique pour les parties contre un bot.
    /// Joue le plus de cartes possible puis attaque la cible
    /// adverse ayant le moins de points de vie.
    /// </summary>
    public sealed class GameAiService
    {
        private readonly GameEngine _engine;

        public GameAiService(
            GameEngine engine)
        {
            _engine = engine;
        }

        /// <summary>
        /// Joue le tour complet du bot (ou plusieurs tours si, par
        /// extraordinaire, il rejoue immédiatement) et termine
        /// systématiquement par un EndTurn.
        /// </summary>
        public async Task<List<string>> PlayTurnAsync(
            GameState state,
            string botUserId,
            CancellationToken cancellationToken = default,
            Func<IReadOnlyList<string>, Task>? onAction = null)
        {
            var logs =
                new List<string>();

            var safety = 0;
            var publishedLogs = 0;

            while (state.Status == GameMatchStatus.InProgress &&
                   state.ActivePlayerId == botUserId &&
                   safety < 50)
            {
                safety++;

                var actionTaken =
                    await TryPlayAnyCardAsync(
                        state,
                        botUserId,
                        logs,
                        cancellationToken);

                if (!actionTaken)
                {
                    actionTaken =
                        await TryUseAnySkillAsync(
                            state,
                            botUserId,
                            logs,
                            cancellationToken);
                }

                if (!actionTaken)
                    break;

                if (onAction != null)
                {
                    await onAction(logs.Skip(publishedLogs).ToList());
                    publishedLogs = logs.Count;
                }
                if (state.Status != GameMatchStatus.InProgress)
                    break;
            }

            if (state.Status == GameMatchStatus.InProgress &&
                state.ActivePlayerId == botUserId)
            {
                var endLogs =
                    await _engine.EndTurnAsync(
                        state,
                        botUserId,
                        cancellationToken);

                logs.AddRange(
                    endLogs);
                if (onAction != null) await onAction(logs.Skip(publishedLogs).ToList());
            }

            return logs;
        }

        private async Task<bool> TryPlayAnyCardAsync(
            GameState state,
            string botUserId,
            List<string> logs,
            CancellationToken cancellationToken)
        {
            var player =
                state.GetPlayer(botUserId);

            var playableHand =
                player.Hand
                    .Where(x => x.UsageCost <= player.Money)
                    .OrderBy(x => x.UsageCost)
                    .ToList();

            foreach (var card in playableHand)
            {
                var slot =
                    card.Type == CardType.Character
                        ? FindFreeSlot(player)
                        : (int?)null;

                if (card.Type == CardType.Character &&
                    slot == null)
                {
                    continue;
                }

                if (card.Type == CardType.Location &&
                    player.Location != null)
                {
                    continue;
                }

                try
                {
                    var playLogs =
                        await _engine.PlayCardAsync(
                            state,
                            botUserId,
                            card.InstanceId,
                            slot,
                            cancellationToken);

                    logs.AddRange(
                        playLogs);

                    return true;
                }
                catch (GameRuleException)
                {
                    // Carte injouable dans ce contexte,
                    // on essaie la suivante.
                    continue;
                }
            }

            return false;
        }

        private async Task<bool> TryUseAnySkillAsync(
            GameState state,
            string botUserId,
            List<string> logs,
            CancellationToken cancellationToken)
        {
            var player =
                state.GetPlayer(botUserId);

            var opponent =
                state.GetOpponent(botUserId);

            var sources =
                GetReadyUnits(player);

            foreach (var source in sources)
            {
                var skill =
                    source.Abilities.FirstOrDefault(
                        x =>
                            x.Type == AbilityType.Skill &&
                            x.UsageCost <= player.Money);

                if (skill == null)
                    continue;

                var target =
                    FindWeakestTarget(
                        opponent);

                try
                {
                    var skillLogs =
                        await _engine.UseAbilityAsync(
                            state,
                            botUserId,
                            source.InstanceId,
                            skill.Id,
                            target?.InstanceId,
                            cancellationToken);

                    logs.AddRange(
                        skillLogs);

                    return true;
                }
                catch (GameRuleException)
                {
                    continue;
                }
            }

            return false;
        }

        private static IEnumerable<GameCardState> GetReadyUnits(
            GamePlayerState player)
        {
            if (!player.Leader.IsExhausted && !player.Leader.IsImprisoned)
                yield return player.Leader;

            foreach (var character in player.Characters)
            {
                if (character != null &&
                    !character.IsExhausted && !character.IsImprisoned)
                {
                    yield return character;
                }
            }
        }

        private static GameCardState? FindWeakestTarget(
            GamePlayerState opponent)
        {
            var candidates =
                opponent.Characters
                    .Where(x =>
                        x != null &&
                        x.CurrentHealth.HasValue)
                    .Cast<GameCardState>()
                    .ToList();

            if (candidates.Count > 0)
            {
                return candidates
                    .OrderBy(x => x.CurrentHealth)
                    .First();
            }

            return opponent.Leader;
        }

        private static int? FindFreeSlot(
            GamePlayerState player)
        {
            for (var i = 0; i < player.Characters.Count; i++)
            {
                if (player.Characters[i] == null)
                    return i;
            }

            return null;
        }
    }
}
