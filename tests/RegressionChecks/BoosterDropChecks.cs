using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game;
using web_tcg.Models.BoosterShop;
using web_tcg.Models.Card;
using web_tcg.Services;

public static class BoosterDropChecks
{
    public static void Run(Action<bool, string> check)
    {
        var generator = new BoosterGenerator(new BoosterRarityGenerator(new Random(1234)));
        var rates = generator.GetDropRates();
        check(Math.Abs(rates.Values.Sum() - 100) < 1e-10, "boosters : les taux moyens totalisent 100 %");
        check(Math.Abs(rates[BoosterRarity.Common] - 64.52) < 1e-10 && Math.Abs(rates[BoosterRarity.FullArt] - .074) < 1e-10,
            "boosters : taux des cinq emplacements, garantie Rare incluse");
        var group = new BoosterDropGroup { Rarity = BoosterRarity.FullArt, Rate = rates[BoosterRarity.FullArt], Cards = new() { new CardViewModel(), new CardViewModel() } };
        check(Math.Abs(group.CardRate - .037) < 1e-10 && new BoosterDropGroup().CardRate == 0,
            "boosters : taux partagé entre les cartes, catégorie vide sans division par zéro");
        var cards = new[]
        {
            new Card { Id=1, Type=CardType.Character, Rarity=CardRarity.Common, ArtType=CardArtType.Normal },
            new Card { Id=2, Type=CardType.Leader, Rarity=CardRarity.UltraRare, ArtType=CardArtType.Normal },
            new Card { Id=3, Type=CardType.Leader, BaseCardId=2, ArtType=CardArtType.FullArt },
            new Card { Id=4, Type=CardType.Character, BaseCardId=1, ArtType=CardArtType.AlternateArt },
            new Card { Id=5, Type=CardType.Character, Rarity=CardRarity.Common, BaseCardId=1, ArtType=CardArtType.Normal }
        }.AsQueryable();
        var eligible = rates.Keys.SelectMany(rarity => BoosterCardSelectionService.FilterCards(cards,rarity)).Select(x => x.Id).ToArray();
        check(eligible.Order().SequenceEqual(new[] {1,2,3,4}) && eligible.Distinct().Count() == eligible.Length,
            "boosters : leaders et variantes regroupés sans doublon selon les règles du tirage");
        var observed = rates.Keys.ToDictionary(rarity => rarity, _ => 0);
        for (var i=0; i<20000; i++)
            foreach (var card in generator.Generate().Cards) observed[card.Rarity]++;
        check(rates.All(rate => Math.Abs(observed[rate.Key]/1000d - rate.Value) < .5),
            "boosters : probabilités affichées cohérentes avec 100 000 tirages réels");
    }
}
