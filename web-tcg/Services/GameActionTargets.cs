using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;

namespace web_tcg.Services;

public static class GameActionTargets
{
    public static string? Refusal(GameState state, GamePlayerState player, GameCardState source, GameAbilityState ability, GameEffectTrigger trigger)
    {
        var effects=ability.Effects.Where(e=>e.Trigger==trigger && NeedsTarget(e.Target)).ToList();
        if(effects.Count==0 || Get(state,player,source,new[]{ability},trigger).Count>0) return null;
        bool Scope(GameCardState candidate, GameTargetType type) => type switch {
            GameTargetType.FriendlyCharacter => candidate.ControllerUserId==player.UserId && candidate.Type==CardType.Character,
            GameTargetType.FriendlyCharacterOrLeader => candidate.ControllerUserId==player.UserId,
            GameTargetType.EnemyCharacter => candidate.ControllerUserId!=player.UserId && candidate.Type==CardType.Character,
            GameTargetType.EnemyLeader => candidate.ControllerUserId!=player.UserId && candidate.Type==CardType.Leader,
            GameTargetType.EnemyCharacterOrLeader => candidate.ControllerUserId!=player.UserId,
            _=>false };
        var candidates=new[]{state.PlayerOne,state.PlayerTwo}.SelectMany(p=>p.Characters.Append(p.Leader)).OfType<GameCardState>()
            .Where(c=>(c.CurrentHealth is null or > 0) && effects.All(e=>Scope(c,e.Target))).ToList();
        if(candidates.Count==0) return "Aucune carte du type et du camp ciblés n'est présente sur le terrain.";
        if(ability.Conditions.Any(c=>c.Type==GameConditionType.TargetRegion) && candidates.All(c=>c.RegionId==0))
            return "Les régions des cibles ne sont pas enregistrées dans cette partie. Lancez une nouvelle partie pour utiliser leurs origines actuelles.";
        var reasons=new List<string>();var resolver=new GameTargetResolver();var evaluator=new GameConditionEvaluator();
        foreach(var candidate in candidates) {
            var failure=evaluator.Failure(state,player,source,candidate,ability.Conditions);
            if(failure!=null) { reasons.Add(failure);continue; }
            foreach(var effect in effects) try { resolver.ResolveCards(state,player,source,effect,candidate.InstanceId); }
                catch(GameRuleException ex) { reasons.Add(ex.Message);break; }
        }
        var distinct=reasons.Distinct().Take(3).ToArray();
        return distinct.Length>0 ? string.Join(" ",distinct) : "Aucune cible ne respecte toutes les contraintes de cette capacité.";
    }
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
