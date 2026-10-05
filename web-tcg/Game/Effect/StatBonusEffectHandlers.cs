using web_tcg.Data.Enums;
using web_tcg.Game.Engine;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects;

public static class GameStatBonuses
{
    public static void Refresh(GameCardState card)
    {
        card.DamageBonus = (int)card.AppliedEffects.Where(e => e.Type == GameEffectType.DamageBonus).Sum(e => e.Value);
        if (!card.MaxHealth.HasValue) return;
        card.BaseHealth ??= card.MaxHealth;
        var maximum = Math.Max(0, card.BaseHealth.Value + (int)card.AppliedEffects.Where(e => e.Type == GameEffectType.HealthBonus).Sum(e => e.Value));
        var difference = maximum - card.MaxHealth.Value;
        card.MaxHealth = maximum;
        if (card.CurrentHealth.HasValue && difference != 0) card.CurrentHealth = Math.Clamp(card.CurrentHealth.Value + difference,0,maximum);
    }
}

public sealed class HealthBonusEffectHandler : IGameEffectHandler
{
    public GameEffectType EffectType => GameEffectType.HealthBonus;
    public Task ExecuteAsync(GameEffectContext context,GameEffectDefinition effect,CancellationToken cancellationToken = default)
    {
        var target = context.Target ?? throw new GameRuleException("Une cible est nécessaire pour le bonus de PV.");
        if (!target.MaxHealth.HasValue) return Task.CompletedTask;
        ShieldEffectHandler.ApplyDurableEffect(target,effect,context,GameGeography.Amount(context,effect));
        GameStatBonuses.Refresh(target);
        return Task.CompletedTask;
    }
}

public sealed class DamageBonusEffectHandler : IGameEffectHandler
{
    public GameEffectType EffectType => GameEffectType.DamageBonus;
    public Task ExecuteAsync(GameEffectContext context,GameEffectDefinition effect,CancellationToken cancellationToken = default)
    {
        var target = context.Target ?? throw new GameRuleException("Une cible est nécessaire pour le bonus de dégâts.");
        ShieldEffectHandler.ApplyDurableEffect(target,effect,context,GameGeography.Amount(context,effect));
        GameStatBonuses.Refresh(target);
        return Task.CompletedTask;
    }
}
