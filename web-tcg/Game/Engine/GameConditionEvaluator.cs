using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Engine
{
    public sealed class GameConditionEvaluator
    {
        public bool AreMet(
            GameState state,
            GamePlayerState player,
            GameCardState source,
            GameCardState? target,
            IEnumerable<GameConditionDefinition> conditions)
        {
            foreach (var condition in conditions)
            {
                if (target == null && IsTargetGeography(condition)) continue;
                var result = Evaluate(
                    state,
                    player,
                    source,
                    target,
                    condition);

                if (condition.Negate)
                    result = !result;

                if (!result)
                    return false;
            }

            return true;
        }

        public static bool IsTargetGeography(GameConditionDefinition condition) => condition.Type is GameConditionType.TargetRegion or GameConditionType.TargetCountry;

        public string? Failure(GameState state, GamePlayerState player, GameCardState source, GameCardState? target, IEnumerable<GameConditionDefinition> conditions)
        {
            var failed = conditions.FirstOrDefault(c => !(target == null && IsTargetGeography(c)) && (Evaluate(state,player,source,target,c) == c.Negate));
            if (failed == null) return null;
            var requirement = failed.Type switch
            {
                GameConditionType.SourceHealthBelow => $"PV de {source.Name} inférieurs à {failed.Value} (actuellement {source.CurrentHealth})",
                GameConditionType.SourceHealthAbove => $"PV de {source.Name} supérieurs à {failed.Value} (actuellement {source.CurrentHealth})",
                GameConditionType.TargetHealthBelow => $"PV de la cible inférieurs à {failed.Value} (actuellement {target?.CurrentHealth})",
                GameConditionType.TargetHealthAbove => $"PV de la cible supérieurs à {failed.Value} (actuellement {target?.CurrentHealth})",
                GameConditionType.PlayerMoneyAtLeast => $"au moins {failed.Value} énergie (actuellement {player.Money})",
                GameConditionType.OpponentHasCharacter => "présence d'un personnage adverse sur le terrain",
                GameConditionType.OpponentHasLocation => "présence d'un lieu adverse",
                GameConditionType.FriendlyCharacterCountAtLeast => $"au moins {failed.Value} personnages alliés sur le terrain",
                GameConditionType.EnemyCharacterCountAtLeast => $"au moins {failed.Value} personnages adverses sur le terrain",
                GameConditionType.SourceRegion => "région de la carte parmi les régions autorisées",
                GameConditionType.SourceCountry => "pays de la carte parmi les pays autorisés",
                GameConditionType.TargetRegion => "région de la cible parmi les régions autorisées",
                GameConditionType.TargetCountry => "pays de la cible parmi les pays autorisés",
                GameConditionType.MatchingFriendlyCharactersAtLeast => $"au moins {failed.Value} personnages alliés des origines choisies (actuellement {GameGeography.Count(state,player,failed.Parameters,scope:1)})",
                GameConditionType.MatchingEnemyCharactersAtLeast => $"au moins {failed.Value} personnages adverses des origines choisies (actuellement {GameGeography.Count(state,player,failed.Parameters,scope:2)})",
                GameConditionType.MatchingCharactersAtLeast => $"au moins {failed.Value} personnages des origines choisies (actuellement {GameGeography.Count(state,player,failed.Parameters,scope:3)})",
                _ => "condition de la capacité"
            };
            return $"Condition non remplie : {(failed.Negate ? "condition inversée — " : "")}{requirement}.";
        }

        private static bool Evaluate(
            GameState state,
            GamePlayerState player,
            GameCardState source,
            GameCardState? target,
            GameConditionDefinition condition)
        {
            var opponent = state.GetOpponent(player.UserId);

            return condition.Type switch
            {
                GameConditionType.None => true,
                GameConditionType.SourceRegion => GameGeography.Ids(condition.Parameters,"regions").Contains(source.RegionId),
                GameConditionType.SourceCountry => source.CountryId.HasValue && GameGeography.Ids(condition.Parameters,"countries").Contains(source.CountryId.Value),
                GameConditionType.TargetRegion => target != null && GameGeography.Ids(condition.Parameters,"regions").Contains(target.RegionId),
                GameConditionType.TargetCountry => target?.CountryId.HasValue == true && GameGeography.Ids(condition.Parameters,"countries").Contains(target.CountryId.Value),
                GameConditionType.MatchingFriendlyCharactersAtLeast => GameGeography.Count(state,player,condition.Parameters,scope:1) >= condition.Value,
                GameConditionType.MatchingEnemyCharactersAtLeast => GameGeography.Count(state,player,condition.Parameters,scope:2) >= condition.Value,
                GameConditionType.MatchingCharactersAtLeast => GameGeography.Count(state,player,condition.Parameters,scope:3) >= condition.Value,

                GameConditionType.SourceHealthBelow =>
                    source.CurrentHealth.HasValue &&
                    source.CurrentHealth.Value < condition.Value,

                GameConditionType.SourceHealthAbove =>
                    source.CurrentHealth.HasValue &&
                    source.CurrentHealth.Value > condition.Value,

                GameConditionType.TargetHealthBelow =>
                    target?.CurrentHealth.HasValue == true &&
                    target.CurrentHealth.Value < condition.Value,

                GameConditionType.TargetHealthAbove =>
                    target?.CurrentHealth.HasValue == true &&
                    target.CurrentHealth.Value > condition.Value,

                GameConditionType.PlayerMoneyAtLeast =>
                    player.Money >= condition.Value,

                GameConditionType.OpponentHasCharacter =>
                    opponent.Characters.Any(x => x != null),

                GameConditionType.OpponentHasLocation =>
                    opponent.Location != null,

                GameConditionType.FriendlyCharacterCountAtLeast =>
                    player.Characters.Count(x => x != null) >= condition.Value,

                GameConditionType.EnemyCharacterCountAtLeast =>
                    opponent.Characters.Count(x => x != null) >= condition.Value,

                _ => false
            };
        }
    }
}
