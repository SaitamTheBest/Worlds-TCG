using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;

namespace web_tcg.Services;

public static class GameActionTargets
{
    public static bool NeedsTarget(GameTargetType type) => type is GameTargetType.FriendlyCharacter or GameTargetType.FriendlyCharacterOrLeader or GameTargetType.EnemyCharacter or GameTargetType.EnemyLeader or GameTargetType.EnemyCharacterOrLeader;

    public static IReadOnlyList<Guid> Get(GameState state, GamePlayerState player, GameCardState source, IEnumerable<GameAbilityState> abilities, params GameEffectTrigger[] triggers)
    {
        var active = abilities.Select(a => (Ability: a, Effects: a.Effects.Where(e => triggers.Contains(e.Trigger)).ToList())).Where(a => a.Effects.Count > 0).ToList();
        if (!active.Any(a => a.Effects.Any(e => NeedsTarget(e.Target)))) return Array.Empty<Guid>();
        var resolver = new GameTargetResolver();
        var evaluator = new GameConditionEvaluator();
        return new[] { state.PlayerOne, state.PlayerTwo }.SelectMany(p => p.Characters.Append(p.Leader)).Where(c => c != null && (c.CurrentHealth == null || c.CurrentHealth > 0))
            .Cast<GameCardState>().Where(candidate => active.All(a =>
            {
                if (!evaluator.AreMet(state,player,source,candidate,a.Ability.Conditions)) return false;
                try { return a.Effects.Where(e => NeedsTarget(e.Target)).All(e => resolver.ResolveCards(state,player,source,e,candidate.InstanceId).Any(c => c.InstanceId == candidate.InstanceId)); }
                catch (GameRuleException) { return false; }
            })).Select(c => c.InstanceId).ToList();
    }
}
