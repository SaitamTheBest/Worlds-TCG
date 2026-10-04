using web_tcg.Models.Card;

namespace web_tcg.Models.BoosterShop;

public sealed class BoosterDropRatesViewModel
{
    public string CardSetName { get; init; } = string.Empty;
    public List<BoosterDropGroup> Groups { get; init; } = new();
    public bool CanOpen => Groups.All(group => group.Cards.Count > 0 || group.Rate == 0);
}

public sealed class BoosterDropGroup
{
    public BoosterRarity Rarity { get; init; }
    public string Name => Rarity switch
    {
        BoosterRarity.FullArt => "Full Art",
        BoosterRarity.AlternateArt => "Alternate Art",
        BoosterRarity.UltraRare => "Ultra rare",
        BoosterRarity.SuperRare => "Super rare",
        BoosterRarity.Leader => "Leader",
        BoosterRarity.Rare => "Rare",
        _ => "Commune"
    };
    public double Rate { get; init; }
    public double CardRate => Cards.Count == 0 ? 0 : Rate / Cards.Count;
    public List<CardViewModel> Cards { get; init; } = new();
}
