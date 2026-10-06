using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;
using web_tcg.Models.Game;

namespace web_tcg.Services;

public static class GameActionPreviewBuilder
{
    public static GameActionPreviewDto Create(GameState state, string userId, Guid? sourceId, int? abilityId, Guid? targetId, int? slot)
    {
        var player = state.GetPlayer(userId);
        if (!sourceId.HasValue) return new() { UserId = userId, Version = state.Version };
        if (state.Status != GameMatchStatus.InProgress || state.ActivePlayerId != userId)
            throw new GameRuleException("Attendez votre tour pour préparer une action.");
        var me = new GameStateProjector().Project(state, userId).Me;
        var hand = me.Hand.FirstOrDefault(c => c.InstanceId == sourceId);
        var source = hand ?? new[] { me.Leader, me.Location }.Concat(me.Characters).FirstOrDefault(c => c?.InstanceId == sourceId);
        if (source == null) throw new GameRuleException("Cette carte ne vous appartient pas ou n'est plus disponible.");
        var ability = abilityId.HasValue ? source.Abilities.FirstOrDefault(a => a.Id == abilityId) : null;
        if (abilityId.HasValue && (hand != null || ability == null || ability.Type == AbilityType.Effect
            || source.IsExhausted || source.IsImprisoned || ability.Cooldown > 0 || ability.UnavailableReason != null
            || ability.UsageCost > me.Money || ability.Type == AbilityType.Skill && me.TurnsPlayed <= 1))
            throw new GameRuleException("Cette capacité n'est pas disponible.");
        if (hand == null && ability == null) throw new GameRuleException("Choisissez une capacité à préparer.");
        if (hand != null && (hand.UsageCost > me.Money || hand.Type is CardType.Leader or CardType.Secret))
            throw new GameRuleException("Cette carte ne peut pas être jouée.");
        if (slot.HasValue && (hand?.Type != CardType.Character || slot < 0 || slot > 3 || me.Characters[slot.Value] != null))
            throw new GameRuleException("Emplacement indisponible.");
        if (targetId.HasValue && !(ability?.TargetIds ?? source.PlayTargetIds).Contains(targetId.Value))
            throw new GameRuleException("Cette cible n'est pas autorisée.");
        // A preparation never exposes a card still in the opponent's hand.
        return new() { UserId = userId, Version = state.Version, SourceId = hand == null ? sourceId : null,
            TargetId = targetId, Slot = slot, Label = ability?.Name ?? "Prépare une carte de sa main", IsAttack = ability?.IsAttack == true };
    }
}
