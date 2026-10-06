using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;
using web_tcg.Models.Game;

namespace web_tcg.Game.Engine;

public static class GameHandCosts
{
    public static bool IsCost(GameConditionDefinition condition) => condition.Type is GameConditionType.RevealHandCards or GameConditionType.DiscardHandCards;
    public static IEnumerable<GameCardState> Eligible(GamePlayerState player, GameCardState source, GameConditionDefinition condition) {
        var types = GameGeography.Ids(condition.Parameters,"types");
        return player.Hand.Where(c => c.InstanceId != source.InstanceId && GameGeography.Matches(c,condition.Parameters)
            && (types.Count == 0 || types.Contains((int)c.Type)));
    }
    public static IReadOnlyList<GameHandCostDto> Requirements(GamePlayerState player, GameCardState source, IEnumerable<GameConditionDefinition> conditions) =>
        conditions.Select((c,i)=>(c,i)).Where(x=>IsCost(x.c)).Select(x=>new GameHandCostDto { ConditionIndex=x.i,
            Discard=x.c.Type==GameConditionType.DiscardHandCards, Count=(int)x.c.Value, EligibleIds=Eligible(player,source,x.c).Select(c=>c.InstanceId).ToArray() }).ToArray();
    public static List<(GameCardState Card, bool Discard)> Validate(GamePlayerState player, GameCardState source,
        IEnumerable<GameConditionDefinition> conditions, IReadOnlyList<GameHandCostSelection>? selections)
    {
        var requirements=Requirements(player,source,conditions);
        selections ??= Array.Empty<GameHandCostSelection>();
        if (selections.Count != requirements.Count || selections.Any(s=>s==null) || selections.Select(s=>s.ConditionIndex).Distinct().Count()!=selections.Count)
            throw new GameRuleException("Choisissez les cartes à dévoiler ou à défausser pour activer cette capacité.");
        var used=new HashSet<Guid>(); var result=new List<(GameCardState,bool)>();
        foreach (var requirement in requirements) {
            var choice=selections.SingleOrDefault(s=>s.ConditionIndex==requirement.ConditionIndex);
            if (requirement.Count < 1 || choice?.CardIds == null || choice.CardIds.Count!=requirement.Count
                || choice.CardIds.Any(id=>!requirement.EligibleIds.Contains(id) || !used.Add(id)))
                throw new GameRuleException($"Choisissez {requirement.Count} carte(s) autorisée(s) à {(requirement.Discard ? "défausser" : "dévoiler")}. Chaque carte ne peut payer qu'un seul coût.");
            foreach(var id in choice.CardIds) result.Add((player.Hand.Single(c=>c.InstanceId==id),requirement.Discard));
        }
        return result;
    }
    public static void Pay(GameState state, GamePlayerState player, List<(GameCardState Card,bool Discard)> costs, List<string> logs) {
        foreach(var (card,discard) in costs) {
            if (discard) { player.Hand.Remove(card); card.Zone=GameCardZone.Graveyard; player.Graveyard.Add(card); }
            else state.ActionReveals.Add(card);
            logs.Add($"{player.DisplayName} {(discard ? "défausse" : "dévoile")} {card.Name} pour activer la capacité.");
        }
    }
    public static IReadOnlyList<GameHandCostSelection> AutoChoose(GamePlayerState player, GameCardState source, IEnumerable<GameConditionDefinition> conditions) {
        var requirements=Requirements(player,source,conditions);
        var slots=requirements.SelectMany(r=>Enumerable.Repeat(r,r.Count)).ToArray();
        var assignments=new Dictionary<Guid,int>();
        bool Assign(int slot,HashSet<Guid> visited) {
            foreach(var id in slots[slot].EligibleIds) {
                if(!visited.Add(id)) continue;
                if(!assignments.TryGetValue(id,out var previous) || Assign(previous,visited)) { assignments[id]=slot;return true; }
            }
            return false;
        }
        for(var i=0;i<slots.Length;i++) if(!Assign(i,new())) throw new GameRuleException("Pas assez de cartes distinctes pour payer les conditions de main.");
        return requirements.Select(r=>new GameHandCostSelection { ConditionIndex=r.ConditionIndex,
            CardIds=assignments.Where(a=>slots[a.Value].ConditionIndex==r.ConditionIndex).Select(a=>a.Key).ToList() }).ToArray();
    }
}
