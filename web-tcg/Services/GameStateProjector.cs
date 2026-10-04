using web_tcg.Game.Runtime;
using web_tcg.Models.Game;
using web_tcg.Data.Enums;

namespace web_tcg.Services
{
    public sealed class GameStateProjector
    {
        public GameStateDto Project(
            GameState state,
            string viewerUserId)
        {
            var me =
                state.GetPlayer(
                    viewerUserId);

            var opponent =
                state.GetOpponent(
                    viewerUserId);

            return new GameStateDto
            {
                MatchId =
                    state.MatchId,

                Version =
                    state.Version,

                Status =
                    state.Status,

                TurnNumber =
                    state.TurnNumber,

                ActivePlayerId =
                    state.ActivePlayerId,

                IsMyTurn =
                    state.ActivePlayerId ==
                    viewerUserId,

                ServerTimeUtc =
                    DateTime.UtcNow,
                ClockAnchorUtc = state.ClockAnchorUtc,

                Me =
                    MapPlayer(
                        state, me,
                        includeHand: true),

                Opponent =
                    MapPlayer(
                        state, opponent,
                        includeHand: false),

                WinnerUserId =
                    state.WinnerUserId,

                EndReason =
                    state.EndReason,
                XpEarned = state.RewardXp.HasValue ? (state.WinnerUserId == viewerUserId ? state.RewardXp.Value : 0) : null,
                CardBalanceEarned = state.RewardCardBalance.HasValue ? (state.WinnerUserId == viewerUserId ? state.RewardCardBalance.Value : 0m) : null
            };
        }

        private static GamePlayerDto MapPlayer(
            GameState state, GamePlayerState player,
            bool includeHand)
        {
            return new GamePlayerDto
            {
                UserId =
                    player.UserId,

                DisplayName =
                    player.DisplayName,

                AvatarUrl =
                    player.AvatarUrl,

                Money =
                    player.Money,

                RemainingSeconds =
                    player.RemainingSeconds,
                TurnsPlayed = player.TurnsPlayed,

                HandCount =
                    player.Hand.Count,

                DeckCount =
                    player.Deck.Count,

                GraveyardCount =
                    player.Graveyard.Count,

                Leader =
                    MapCard(
                        state, player, player.Leader),

                Location =
                    player.Location == null
                        ? null
                        : MapCard(state, player, player.Location),

                Characters =
                    player.Characters
                        .Select(x =>
                            x == null
                                ? null
                                : MapCard(state, player, x))
                        .ToList(),

                Hand =
                    includeHand
                        ? player.Hand
                            .Select(c => MapCard(state, player, c))
                            .ToList()
                        : Array.Empty<GameCardDto>()
            };
        }

        private static GameCardDto MapCard(
            GameState state, GamePlayerState player, GameCardState card)
        {
            return new GameCardDto
            {
                InstanceId =
                    card.InstanceId,

                CardId =
                    card.CardId,

                Name =
                    card.Name,

                Type =
                    card.Type,

                ImageUrl =
                    card.ImageUrl,

                UsageCost =
                    card.UsageCost,

                MaxHealth =
                    card.MaxHealth,

                CurrentHealth =
                    card.CurrentHealth,
                DamageMultiplier = card.DamageMultiplier,
                ExtraSkillUses = card.ExtraSkillUses,
                DamageBonus = card.DamageBonus,

                IsExhausted =
                    card.IsExhausted,

                IsBlocking =
                    card.IsBlocking,

                HasShield =
                    card.HasShield,

                IsStealthed =
                    card.IsStealthed,
                PlayTargetIds = GameActionTargets.Get(state, player, card, card.Abilities, GameEffectTrigger.OnPlay, GameEffectTrigger.Passive),
                PlayTargetTypes = card.Abilities.SelectMany(a => a.Effects)
                    .Where(e => e.Trigger is GameEffectTrigger.OnPlay or GameEffectTrigger.Passive)
                    .Select(e => e.Target).Distinct().ToList(),

                Abilities =
                    card.Abilities
                        .Select(x =>
                            new GameAbilityDto
                            {
                                Id =
                                    x.Id,
                                UnavailableReason = new web_tcg.Game.Engine.GameConditionEvaluator().Failure(state,player,card,null,x.Conditions.Where(c => c.Type is not (GameConditionType.TargetHealthBelow or GameConditionType.TargetHealthAbove or GameConditionType.TargetRegion or GameConditionType.TargetCountry))),
                                TargetIds = GameActionTargets.Get(state, player, card, new[] { x }, x.Type == AbilityType.Skill ? GameEffectTrigger.OnSkill : GameEffectTrigger.OnTalent),
                                IsAttack = x.Effects.Any(e => e.Type == GameEffectType.Damage && e.Trigger == (x.Type == AbilityType.Skill ? GameEffectTrigger.OnSkill : GameEffectTrigger.OnTalent)),

                                Type =
                                    x.Type,

                                Name =
                                    x.Name,

                                Description =
                                    x.Description,

                                UsageCost =
                                    x.UsageCost,
                                TargetTypes = x.Effects.Where(effect => effect.Trigger ==
                                    (x.Type == web_tcg.Data.Enums.AbilityType.Skill ? web_tcg.Data.Enums.GameEffectTrigger.OnSkill : web_tcg.Data.Enums.GameEffectTrigger.OnTalent))
                                    .Select(effect => effect.Target).Distinct().ToList(),

                                Cooldown =
                                    card.AbilityCooldowns
                                        .GetValueOrDefault(
                                            x.Id)
                            })
                        .ToList()
            };
        }
    }
}



