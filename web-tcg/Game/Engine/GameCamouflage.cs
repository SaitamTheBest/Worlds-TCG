using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Engine;

public static class GameCamouflage
{
    // Keep the granted effect intact while inactive, so it can reactivate.
    public static bool IsActive(GameState state, GameCardState card)
    {
        if (!card.IsStealthed) return false;
        if (card.Type != CardType.Character) return true;
        if (card.Zone != GameCardZone.CharacterBoard || card.CurrentHealth is null or <= 0 || card.IsKnockedOut) return false;
        return state.GetPlayer(card.ControllerUserId).Characters.Any(other =>
            other != null && other.InstanceId != card.InstanceId && other.Type == CardType.Character &&
            other.Zone == GameCardZone.CharacterBoard && other.CurrentHealth > 0 && !other.IsKnockedOut);
    }
}
