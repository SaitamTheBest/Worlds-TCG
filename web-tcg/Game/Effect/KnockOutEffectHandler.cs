using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects;

public sealed class KnockOutEffectHandler : IGameEffectHandler
{
    public GameEffectType EffectType => GameEffectType.KnockOut;

    public Task ExecuteAsync(GameEffectContext context, GameEffectDefinition effect, CancellationToken cancellationToken = default)
    {
        var target = context.Target ?? throw new GameRuleException("KO nécessite une carte sur le terrain.");
        if (target.Zone is not (GameCardZone.CharacterBoard or GameCardZone.Location))
            throw new GameRuleException("KO cible un personnage ou un lieu sur le terrain.");
        if (target.Zone == GameCardZone.Location)
        {
            var owner = context.State.GetPlayer(target.ControllerUserId);
            owner.Location = null;
            target.Zone = GameCardZone.Graveyard;
            target.BoardSlot = null;
            owner.Graveyard.Add(target);
        }
        else
        {
            // Death resolution executes death triggers and frees the occupied slot.
            target.IsKnockedOut = true;
            target.CurrentHealth = 0;
        }
        context.Logs.Add($"{target.Name} est mis KO.");
        return Task.CompletedTask;
    }
}
