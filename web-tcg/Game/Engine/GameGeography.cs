using System.Text.Json;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Engine;

public static class GameGeography
{
    public static List<int> Ids(IReadOnlyDictionary<string,string> parameters, string key) =>
        parameters.TryGetValue(key,out var value) ? value.Split(',',StringSplitOptions.RemoveEmptyEntries).Select(v => int.TryParse(v,out var id) ? id : 0).Where(id => id > 0).Distinct().ToList() : new();
    public static Dictionary<string,string> Parse(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string,string>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }
    public static bool Matches(GameCardState card, IReadOnlyDictionary<string,string> parameters, string prefix = "")
    {
        var parties = Ids(parameters,prefix + "parties");
        var orientations = Ids(parameters,prefix + "orientations");
        var regions = Ids(parameters,prefix + "regions"); var countries = Ids(parameters,prefix + "countries");
        return (orientations.Count == 0 || card.PoliticalPartyId.HasValue && card.PoliticalPartyOrientation.HasValue && orientations.Contains((int)card.PoliticalPartyOrientation.Value))
            && (parties.Count == 0 || card.PoliticalPartyId.HasValue && parties.Contains(card.PoliticalPartyId.Value)) && (regions.Count == 0 || regions.Contains(card.RegionId)) && (countries.Count == 0 || card.CountryId.HasValue && countries.Contains(card.CountryId.Value));
    }
    public static IEnumerable<GameCardState> Characters(GameState state, GamePlayerState player, int scope) =>
        (scope == 1 ? player.Characters : scope == 2 ? state.GetOpponent(player.UserId).Characters : player.Characters.Concat(state.GetOpponent(player.UserId).Characters))
        .OfType<GameCardState>().Where(c => c.CurrentHealth is null or > 0);
    public static int Count(GameState state, GamePlayerState player, IReadOnlyDictionary<string,string> parameters, string prefix = "", int scope = 1) =>
        Characters(state,player,scope).Count(c => Matches(c,parameters,prefix));
    public static decimal Amount(web_tcg.Game.Effects.GameEffectContext context, GameEffectDefinition effect) =>
        effect.Parameters.GetValueOrDefault("scaleByCount") == "true"
            ? effect.Value * Count(context.State,context.Player,effect.Parameters,"count",int.TryParse(effect.Parameters.GetValueOrDefault("countScope"),out var scope) ? scope : 1)
            : effect.Value;
}
