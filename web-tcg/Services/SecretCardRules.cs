using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Models.Admin;

namespace web_tcg.Services;

public static class SecretCardRules
{
    public static bool IsNonPlayable(Card card) => card.Type == CardType.Secret || card.CollectionNumber == 0;

    public static void Normalize(CreateCardViewModel input)
    {
        if (input.Type != CardType.Secret) return;
        input.CollectionNumber = 0;
        input.Rarity = CardRarity.Gold;
        input.UsageCost = 0;
        input.HealthPoints = null;
        input.Abilities.Clear();
        input.Variants.Clear();
    }
}
