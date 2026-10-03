using web_tcg.Data.Enums;
using web_tcg.Game.Effects;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Engine
{
    public sealed class GameEngine
    {
        public const int InactivityLimitSeconds = 300;

        private readonly GameEffectEngine _effects;
        private readonly GameConditionEvaluator _conditions;

        public GameEngine(
            GameEffectEngine effects,
            GameConditionEvaluator conditions)
        {
            _effects = effects;
            _conditions = conditions;
        }

        public async Task<List<string>> StartMatchAsync(
            GameState state,
            CancellationToken cancellationToken = default)
        {
            var logs = new List<string>();

            Shuffle(state.PlayerOne.Deck);
            Shuffle(state.PlayerTwo.Deck);

            Draw(state.PlayerOne, 5);
            Draw(state.PlayerTwo, 5);

            state.FirstPlayerId =
                Random.Shared.Next(0, 2) == 0
                    ? state.PlayerOne.UserId
                    : state.PlayerTwo.UserId;

            state.ActivePlayerId =
                state.FirstPlayerId;

            state.Status =
                GameMatchStatus.InProgress;

            InitializeLeaderCooldowns(
                state.PlayerOne.Leader);

            InitializeLeaderCooldowns(
                state.PlayerTwo.Leader);

            foreach (var owner in new[] { state.PlayerOne, state.PlayerTwo })
            {
                await ExecuteTriggerAsync(state, owner, owner.Leader, GameEffectTrigger.OnPlay, null, logs, cancellationToken);
                await ExecuteTriggerAsync(state, owner, owner.Leader, GameEffectTrigger.Passive, null, logs, cancellationToken);
            }

            logs.Add(
                $"{state.GetPlayer(state.FirstPlayerId).DisplayName} remporte le pile ou face.");

            await BeginTurnAsync(
                state,
                state.GetPlayer(state.ActivePlayerId),
                logs,
                cancellationToken);

            return logs;
        }

        public void ApplyTimeouts(
            GameState state,
            DateTime now)
        {
            if (state.Status != GameMatchStatus.InProgress)
                return;

            var active =
                state.GetPlayer(
                    state.ActivePlayerId);

            var elapsed =
                Math.Max(
                    0,
                    (int)Math.Floor(
                        (now - state.ClockAnchorUtc).TotalSeconds));

            if (elapsed >= active.RemainingSeconds)
            {
                active.RemainingSeconds = 0;

                Finish(
                    state,
                    state.GetOpponent(active.UserId).UserId,
                    GameEndReason.TimeExpired);

                return;
            }

            var inactiveSeconds =
                (now - state.LastActionAtUtc)
                .TotalSeconds;

            if (inactiveSeconds >= InactivityLimitSeconds)
            {
                Finish(
                    state,
                    state.GetOpponent(active.UserId).UserId,
                    GameEndReason.Inactivity);
            }
        }

        public void ConsumeActiveClock(
            GameState state,
            DateTime now)
        {
            if (state.Status != GameMatchStatus.InProgress)
                return;

            var active =
                state.GetPlayer(
                    state.ActivePlayerId);

            var elapsed =
                Math.Max(
                    0,
                    (int)Math.Floor(
                        (now - state.ClockAnchorUtc).TotalSeconds));

            active.RemainingSeconds =
                Math.Max(
                    0,
                    active.RemainingSeconds - elapsed);

            state.ClockAnchorUtc = now;
        }

        public async Task<List<string>> PlayCardAsync(
            GameState state,
            string userId,
            Guid cardInstanceId,
            int? slot,
            CancellationToken cancellationToken = default,
            Guid? targetInstanceId = null)
        {
            EnsureActivePlayer(state, userId);

            var player =
                state.GetPlayer(userId);

            var card =
                player.Hand.FirstOrDefault(
                    x => x.InstanceId == cardInstanceId)
                ?? throw new GameRuleException(
                    "Cette carte n'est pas dans votre main.");

            if (card.Type == CardType.Leader)
            {
                throw new GameRuleException(
                    "Un Leader ne peut pas être joué depuis la main.");
            }

            EnsureCanPay(
                player,
                card.UsageCost);

            // Validate the destination before spending energy or removing the card.
            if (card.Type == CardType.Character &&
                (!slot.HasValue || slot.Value < 0 || slot.Value > 3))
                throw new GameRuleException("Emplacement de personnage invalide.");
            if (card.Type == CardType.Character && player.Characters[slot!.Value] != null)
                throw new GameRuleException("Cet emplacement est déjà occupé.");
            if (card.Type == CardType.Location && player.Location != null)
                throw new GameRuleException("Vous possédez déjà un lieu sur le terrain.");
            if (card.Type != CardType.Character && card.Type != CardType.Location && card.Type != CardType.PoliticalDecision)
                throw new GameRuleException("Ce type de carte ne peut pas être joué.");

            var playContext = new GameEffectContext { State = state, Player = player, Source = card, RequestedTargetId = targetInstanceId };
            foreach (var ability in card.Abilities.Where(a => a.Effects.Any(e => e.Trigger == GameEffectTrigger.OnPlay)))
            {
                if (!_conditions.AreMet(state, player, card, targetInstanceId.HasValue ? state.FindCard(targetInstanceId.Value) : null, ability.Conditions))
                    throw new GameRuleException(_conditions.Failure(state,player,card,targetInstanceId.HasValue ? state.FindCard(targetInstanceId.Value) : null,ability.Conditions)!);
                _effects.ValidateTargets(playContext, ability.Effects, GameEffectTrigger.OnPlay);
            }

            var logs =
                new List<string>();

            player.Money -=
                card.UsageCost;

            player.Hand.Remove(
                card);

            switch (card.Type)
            {
                case CardType.Character:
                    PlayCharacter(
                        player,
                        card,
                        slot);

                    logs.Add(
                        $"{player.DisplayName} joue {card.Name}.");

                    await ExecuteTriggerAsync(
                        state,
                        player,
                        card,
                        GameEffectTrigger.OnPlay,
                        targetInstanceId,
                        logs,
                        cancellationToken);
                    break;

                case CardType.Location:
                    if (player.Location != null)
                    {
                        throw new GameRuleException(
                            "Vous possédez déjà un lieu sur le terrain.");
                    }

                    card.Zone =
                        GameCardZone.Location;

                    player.Location =
                        card;

                    logs.Add(
                        $"{player.DisplayName} pose le lieu {card.Name}.");

                    await ExecuteTriggerAsync(
                        state,
                        player,
                        card,
                        GameEffectTrigger.OnPlay,
                        targetInstanceId,
                        logs,
                        cancellationToken);
                    break;

                case CardType.PoliticalDecision:
                    await ExecuteTriggerAsync(
                        state,
                        player,
                        card,
                        GameEffectTrigger.OnPlay,
                        targetInstanceId,
                        logs,
                        cancellationToken);

                    card.Zone =
                        GameCardZone.Graveyard;

                    player.Graveyard.Add(
                        card);

                    logs.Add(
                        $"{card.Name} est envoyée au cimetière.");
                    break;

                default:
                    throw new GameRuleException(
                        "Type de carte non jouable.");
            }

            if (card.Type is CardType.Character or CardType.Location)
                await ExecuteTriggerAsync(state, player, card, GameEffectTrigger.Passive, targetInstanceId, logs, cancellationToken);

            await ResolveDeathsAsync(state, logs, cancellationToken);
            return logs;
        }

        public Task<List<string>> BlockAsync(
            GameState state,
            string userId,
            Guid sourceInstanceId,
            CancellationToken cancellationToken = default)
        {
            EnsureActivePlayer(
                state,
                userId);

            var player =
                state.GetPlayer(userId);

            EnsureNormalGamePhase(
                player);

            var source =
                FindOwnUnit(
                    player,
                    sourceInstanceId);

            EnsureReady(
                source);

            if (source.Type == CardType.Leader)
                throw new GameRuleException("Un Leader ne peut pas bloquer une attaque.");

            source.IsBlocking = true;
            source.BlockExpiresAfterTurn =
                state.TurnNumber + 1;

            Exhaust(
                player,
                source);

            return Task.FromResult(
                new List<string>
                {
                    $"{source.Name} passe en position de blocage."
                });
        }

        public async Task<List<string>> UseAbilityAsync(
            GameState state,
            string userId,
            Guid sourceInstanceId,
            int abilityId,
            Guid? targetInstanceId,
            CancellationToken cancellationToken = default)
        {
            EnsureActivePlayer(
                state,
                userId);

            var player =
                state.GetPlayer(userId);

            var source =
                FindOwnUnit(
                    player,
                    sourceInstanceId);

            var ability =
                source.Abilities.FirstOrDefault(
                    x => x.Id == abilityId)
                ?? throw new GameRuleException(
                    "Capacité introuvable.");

            if (ability.Type == AbilityType.Effect)
            {
                throw new GameRuleException(
                    "Un effet passif ne peut pas être activé manuellement.");
            }

            var requestedTarget =
                targetInstanceId.HasValue
                    ? state.FindCard(targetInstanceId.Value)
                    : null;

            if (!_conditions.AreMet(
                    state,
                    player,
                    source,
                    requestedTarget,
                    ability.Conditions))
            {
                throw new GameRuleException(
                    _conditions.Failure(state,player,source,requestedTarget,ability.Conditions)!);
            }

            EnsureCanPay(
                player,
                ability.UsageCost);

            GameEffectTrigger trigger;

            switch (ability.Type)
            {
                case AbilityType.Skill:
                    EnsureNormalGamePhase(
                        player);

                    EnsureReady(
                        source);

                    trigger =
                        GameEffectTrigger.OnSkill;
                    break;

                case AbilityType.Trait:
                    EnsureReady(source);
                    var cooldown =
                        source.AbilityCooldowns
                            .GetValueOrDefault(
                                ability.Id);

                    if (cooldown > 0)
                    {
                        throw new GameRuleException(
                            $"Talent disponible dans {cooldown} tour(s).");
                    }

                    trigger =
                        GameEffectTrigger.OnTalent;
                    break;

                default:
                    throw new GameRuleException(
                        "Type de capacité non supporté.");
            }

            var context =
                new GameEffectContext
                {
                    State = state,
                    Player = player,
                    Source = source,
                    RequestedTargetId = targetInstanceId,
                    TargetConditions = ability.Conditions.Where(GameConditionEvaluator.IsTargetGeography).ToList()
                };

            _effects.ValidateTargets(context, ability.Effects, trigger);
            player.Money -= ability.UsageCost;

            await _effects.ExecuteAsync(
                context,
                ability.Effects,
                trigger,
                cancellationToken);

            if (ability.Type == AbilityType.Skill)
            {
                source.SkillUsesThisTurn++;

                if (source.SkillUsesThisTurn >=
                    1 + source.ExtraSkillUses)
                {
                    Exhaust(
                        player,
                        source);
                }
            }
            else
            {
                source.AbilityCooldowns[ability.Id] =
                    ability.CooldownTurns;
            }

            var logs =
                new List<string>
                {
                    $"{player.DisplayName} utilise {ability.Name} avec {source.Name}."
                };

            logs.AddRange(
                context.Logs);

            await ResolveDeathsAsync(
                state,
                logs,
                cancellationToken);

            return logs;
        }

        public async Task<List<string>> EndTurnAsync(
            GameState state,
            string userId,
            CancellationToken cancellationToken = default)
        {
            EnsureActivePlayer(
                state,
                userId);

            var player =
                state.GetPlayer(userId);

            var logs =
                new List<string>();

            await ExecuteBoardTriggerAsync(
                state,
                player,
                GameEffectTrigger.OnTurnEnd,
                logs,
                cancellationToken);

            await ResolveDeathsAsync(
                state,
                logs,
                cancellationToken);

            if (state.Status == GameMatchStatus.Finished)
                return logs;

            ClearExpiredBlocks(
                state);

            var opponent =
                state.GetOpponent(userId);

            state.ActivePlayerId =
                opponent.UserId;

            await BeginTurnAsync(
                state,
                opponent,
                logs,
                cancellationToken);

            return logs;
        }

        public List<string> Surrender(
            GameState state,
            string userId)
        {
            if (state.Status != GameMatchStatus.InProgress)
            {
                throw new GameRuleException(
                    "Cette partie est déjà terminée.");
            }

            var opponent =
                state.GetOpponent(userId);

            Finish(
                state,
                opponent.UserId,
                GameEndReason.Surrender);

            return new List<string>
            {
                $"{state.GetPlayer(userId).DisplayName} abandonne la partie."
            };
        }

        private async Task BeginTurnAsync(
            GameState state,
            GamePlayerState player,
            List<string> logs,
            CancellationToken cancellationToken)
        {
            state.TurnNumber++;
            player.TurnsPlayed++;

            RefreshPlayerCards(
                player);

            DecreaseCooldowns(
                player);

            ExpireEffects(
                player);

            var drawn =
                Draw(
                    player,
                    1);

            var moneyGain =
                player.TurnsPlayed == 1 &&
                player.UserId == state.FirstPlayerId
                    ? 10
                    : 20;

            player.Money +=
                moneyGain;

            var now =
                DateTime.UtcNow;

            state.TurnStartedAtUtc = now;
            state.ClockAnchorUtc = now;
            state.LastActionAtUtc = now;

            logs.Add(
                $"Tour {state.TurnNumber} : {player.DisplayName} pioche {drawn} carte(s) et gagne {moneyGain} énergie.");

            await ExecuteBoardTriggerAsync(
                state,
                player,
                GameEffectTrigger.OnTurnStart,
                logs,
                cancellationToken);

            await ResolveDeathsAsync(
                state,
                logs,
                cancellationToken);
        }

        private async Task ExecuteBoardTriggerAsync(
            GameState state,
            GamePlayerState player,
            GameEffectTrigger trigger,
            List<string> logs,
            CancellationToken cancellationToken)
        {
            var cards =
                GetUnits(player)
                    .Concat(
                        player.Location != null
                            ? new[] { player.Location }
                            : Array.Empty<GameCardState>())
                    .ToList();

            foreach (var card in cards)
            {
                await ExecuteTriggerAsync(
                    state,
                    player,
                    card,
                    trigger,
                    null,
                    logs,
                    cancellationToken);
            }
        }

        private async Task ExecuteTriggerAsync(
            GameState state,
            GamePlayerState player,
            GameCardState source,
            GameEffectTrigger trigger,
            Guid? target,
            List<string> logs,
            CancellationToken cancellationToken)
        {
            foreach (var ability in source.Abilities)
            {
                if (trigger != GameEffectTrigger.OnPlay && !_conditions.AreMet(state, player, source, target.HasValue ? state.FindCard(target.Value) : null, ability.Conditions))
                    continue;
                var context =
                    new GameEffectContext
                    {
                        State = state,
                        Player = player,
                        Source = source,
                        RequestedTargetId = target,
                        TargetConditions = ability.Conditions.Where(GameConditionEvaluator.IsTargetGeography).ToList()
                    };

                await _effects.ExecuteAsync(
                    context,
                    ability.Effects,
                    trigger,
                    cancellationToken);

                logs.AddRange(
                    context.Logs);
            }
        }

        private async Task ResolveDeathsAsync(
            GameState state,
            List<string> logs,
            CancellationToken cancellationToken)
        {
            await ReconcilePassiveEffectsAsync(state, logs, cancellationToken);
            foreach (var player in new[]
                     {
                         state.PlayerOne,
                         state.PlayerTwo
                     })
            {
                for (var slot = 0;
                     slot < player.Characters.Count;
                     slot++)
                {
                    var card =
                        player.Characters[slot];

                    if (card?.CurrentHealth is null ||
                        card.CurrentHealth > 0)
                    {
                        continue;
                    }

                    await ExecuteTriggerAsync(
                        state,
                        player,
                        card,
                        GameEffectTrigger.OnDeath,
                        null,
                        logs,
                        cancellationToken);

                    if (card.CurrentHealth > 0)
                        continue;

                    player.Characters[slot] =
                        null;

                    card.BoardSlot =
                        null;

                    card.Zone =
                        GameCardZone.Graveyard;

                    player.Graveyard.Add(
                        card);

                    logs.Add(
                        $"{card.Name} est envoyé au cimetière.");
                }

                var leader =
                    player.Leader;

                if (leader.CurrentHealth is null ||
                    leader.CurrentHealth > 0)
                {
                    continue;
                }

                await ExecuteTriggerAsync(
                    state,
                    player,
                    leader,
                    GameEffectTrigger.OnDeath,
                    null,
                    logs,
                    cancellationToken);

                if (leader.CurrentHealth > 0)
                    continue;

                var winner =
                    state.GetOpponent(
                        player.UserId);

                Finish(
                    state,
                    winner.UserId,
                    GameEndReason.LeaderDefeated);

                logs.Add(
                    $"{player.DisplayName} perd son Leader.");
            }
            if (state.Status != GameMatchStatus.Finished)
            {
                await ReconcilePassiveEffectsAsync(state, logs, cancellationToken);
                if (new[] { state.PlayerOne, state.PlayerTwo }.SelectMany(GetUnits).Any(c => c.CurrentHealth <= 0))
                    await ResolveDeathsAsync(state, logs, cancellationToken);
            }
        }

        private async Task ReconcilePassiveEffectsAsync(GameState state, List<string> logs, CancellationToken cancellationToken)
        {
            var providers = new[] { state.PlayerOne, state.PlayerTwo }
                .SelectMany(p => GetUnits(p).Concat(p.Location != null ? new[] { p.Location } : Array.Empty<GameCardState>())).Where(c => c.CurrentHealth is null or > 0).ToList();
            var providersById = providers.ToDictionary(c => c.InstanceId);
            foreach (var unit in providers.Where(c => c.Type is CardType.Character or CardType.Leader))
            {
                var hadBlockAura = unit.AppliedEffects.Any(e => e.SourceInstanceId.HasValue && e.Type == GameEffectType.Block);
                unit.AppliedEffects.RemoveAll(e => e.SourceInstanceId.HasValue &&
                    (!providersById.TryGetValue(e.SourceInstanceId.Value,out var provider) || !provider.Abilities.Any(a =>
                        a.Effects.Any(d => d.Type == e.Type && (e.DefinitionKey == null ? d.Value == e.Value : System.Text.Json.JsonSerializer.Serialize(d) == e.DefinitionKey) && GameGeography.Matches(unit,d.Parameters)) &&
                        _conditions.AreMet(state,state.GetPlayer(provider.OwnerUserId),provider,unit,a.Conditions))));
                if (hadBlockAura && !unit.AppliedEffects.Any(e => e.Type == GameEffectType.Block)) { unit.IsBlocking = false; unit.BlockExpiresAfterTurn = null; }
                unit.HasShield = unit.HasShield && unit.AppliedEffects.Any(e => e.Type == GameEffectType.Shield);
                unit.IsStealthed = unit.AppliedEffects.Any(e => e.Type == GameEffectType.Stealth);
                unit.ExtraSkillUses = unit.AppliedEffects.Where(e => e.Type == GameEffectType.ExtraAttack).Select(e => Math.Max(1,(int)e.Value)).DefaultIfEmpty(0).Max();
                unit.DamageMultiplier = unit.AppliedEffects.Where(e => e.Type == GameEffectType.DamageMultiplier).Select(e => Math.Max(1m,e.Value)).DefaultIfEmpty(1m).Max();
            }
            foreach (var player in new[] { state.PlayerOne, state.PlayerTwo })
                foreach (var card in GetUnits(player)) GameStatBonuses.Refresh(card);
            foreach (var source in providers)
            {
                if (!source.Abilities.Any(a => a.Effects.Any(e => e.Trigger == GameEffectTrigger.Passive && e.Type is GameEffectType.Block or GameEffectType.Shield or GameEffectType.Stealth or GameEffectType.ExtraAttack or GameEffectType.DamageMultiplier or GameEffectType.HealthBonus or GameEffectType.DamageBonus))) continue;
                var owner = state.GetPlayer(source.OwnerUserId);
                foreach (var ability in source.Abilities)
                {
                    if (!_conditions.AreMet(state, owner, source, null, ability.Conditions)) continue;
                    var aura = ability.Effects.Where(e => e.Trigger == GameEffectTrigger.Passive && e.Target is not (GameTargetType.FriendlyCharacter or GameTargetType.FriendlyCharacterOrLeader or GameTargetType.EnemyCharacter or GameTargetType.EnemyCharacterOrLeader)
                        && e.Type is GameEffectType.Block or GameEffectType.Shield or GameEffectType.Stealth or GameEffectType.ExtraAttack or GameEffectType.DamageMultiplier or GameEffectType.HealthBonus or GameEffectType.DamageBonus).ToList();
                    if (aura.Count == 0) continue;
                    var context = new GameEffectContext { State = state, Player = owner, Source = source, TargetConditions = ability.Conditions.Where(GameConditionEvaluator.IsTargetGeography).ToList() };
                    await _effects.ExecuteAsync(context,aura,GameEffectTrigger.Passive,cancellationToken);
                    logs.AddRange(context.Logs);
                }
            }
        }

        private static void PlayCharacter(
            GamePlayerState player,
            GameCardState card,
            int? slot)
        {
            if (!slot.HasValue ||
                slot.Value < 0 ||
                slot.Value > 3)
            {
                throw new GameRuleException(
                    "Emplacement de personnage invalide.");
            }

            if (player.Characters[slot.Value] != null)
            {
                throw new GameRuleException(
                    "Cet emplacement est déjà occupé.");
            }

            card.Zone =
                GameCardZone.CharacterBoard;

            card.BoardSlot =
                slot.Value;

            card.IsExhausted =
                true;

            card.ReadyOnOwnerTurn =
                player.TurnsPlayed + 1;

            foreach (var ability in card.Abilities
                         .Where(x => x.Type == AbilityType.Trait))
            {
                card.AbilityCooldowns[ability.Id] =
                    ability.CooldownTurns;
            }

            player.Characters[slot.Value] =
                card;
        }

        private static void RefreshPlayerCards(
            GamePlayerState player)
        {
            foreach (var card in GetUnits(player))
            {
                card.SkillUsesThisTurn = 0;

                if (card.IsExhausted &&
                    card.ReadyOnOwnerTurn <= player.TurnsPlayed)
                {
                    card.IsExhausted = false;
                }
            }
        }

        private static void DecreaseCooldowns(
            GamePlayerState player)
        {
            foreach (var card in GetUnits(player))
            {
                foreach (var abilityId in
                         card.AbilityCooldowns.Keys.ToList())
                {
                    if (card.AbilityCooldowns[abilityId] > 0)
                    {
                        card.AbilityCooldowns[abilityId]--;
                    }
                }
            }
        }

        private static void ExpireEffects(
            GamePlayerState player)
        {
            foreach (var card in GetUnits(player))
            {
                card.AppliedEffects.RemoveAll(
                    x =>
                        x.ExpiresOnOwnerTurn.HasValue &&
                        x.ExpiresOnOwnerTurn.Value <=
                        player.TurnsPlayed);
                GameStatBonuses.Refresh(card);

                card.IsStealthed =
                    card.AppliedEffects.Any(
                        x =>
                            x.Type ==
                            GameEffectType.Stealth);

                card.HasShield =
                    card.HasShield &&
                    card.AppliedEffects.Any(
                        x =>
                            x.Type ==
                            GameEffectType.Shield);

                card.ExtraSkillUses =
                    card.AppliedEffects
                        .Where(x =>
                            x.Type ==
                            GameEffectType.ExtraAttack)
                        .Select(x =>
                            Math.Max(
                                1,
                                (int)x.Value))
                        .DefaultIfEmpty(0)
                        .Max();

                card.DamageMultiplier =
                    card.AppliedEffects
                        .Where(x =>
                            x.Type ==
                            GameEffectType.DamageMultiplier)
                        .Select(x =>
                            Math.Max(
                                1m,
                                x.Value))
                        .DefaultIfEmpty(1m)
                        .Max();
            }
        }

        private static void ClearExpiredBlocks(
            GameState state)
        {
            foreach (var card in state.EnumerateCards())
            {
                if (!card.IsBlocking ||
                    !card.BlockExpiresAfterTurn.HasValue)
                {
                    continue;
                }

                if (card.BlockExpiresAfterTurn.Value <=
                    state.TurnNumber)
                {
                    card.IsBlocking = false;
                    card.BlockExpiresAfterTurn = null;
                }
            }
        }

        private static GameCardState FindOwnUnit(
            GamePlayerState player,
            Guid instanceId)
        {
            var source =
                GetUnits(player)
                    .FirstOrDefault(
                        x =>
                            x.InstanceId ==
                            instanceId);

            return source
                ?? throw new GameRuleException(
                    "Carte introuvable sur votre terrain.");
        }

        private static IEnumerable<GameCardState> GetUnits(
            GamePlayerState player)
        {
            yield return player.Leader;

            foreach (var character in player.Characters)
            {
                if (character != null)
                    yield return character;
            }
        }

        private static void EnsureActivePlayer(
            GameState state,
            string userId)
        {
            if (state.Status != GameMatchStatus.InProgress)
            {
                throw new GameRuleException(
                    "La partie n'est pas en cours.");
            }

            if (state.ActivePlayerId != userId)
            {
                throw new GameRuleException(
                    "Ce n'est pas votre tour.");
            }
        }

        private static void EnsureNormalGamePhase(
            GamePlayerState player)
        {
            if (player.TurnsPlayed <= 1)
            {
                throw new GameRuleException(
                    "Les actions normales ne sont pas disponibles pendant votre premier tour.");
            }
        }

        private static void EnsureReady(
            GameCardState source)
        {
            if (source.IsExhausted)
            {
                throw new GameRuleException(
                    $"{source.Name} est épuisé.");
            }
        }

        private static void EnsureCanPay(
            GamePlayerState player,
            int cost)
        {
            if (player.Money < cost)
            {
                throw new GameRuleException(
                    $"Énergie insuffisante : {cost} nécessaire.");
            }
        }

        private static void Exhaust(
            GamePlayerState player,
            GameCardState card)
        {
            card.IsExhausted = true;
            card.ReadyOnOwnerTurn =
                player.TurnsPlayed + 1;
        }

        private static int Draw(
            GamePlayerState player,
            int count)
        {
            var drawn = 0;

            for (var i = 0;
                 i < count && player.Deck.Count > 0;
                 i++)
            {
                var card =
                    player.Deck[0];

                player.Deck.RemoveAt(0);

                card.Zone =
                    GameCardZone.Hand;

                player.Hand.Add(
                    card);

                drawn++;
            }

            return drawn;
        }

        private static void InitializeLeaderCooldowns(
            GameCardState leader)
        {
            foreach (var ability in leader.Abilities
                         .Where(x =>
                             x.Type ==
                             AbilityType.Trait))
            {
                /*
                 * +1 pour que "cooldown 2" signifie
                 * réellement deux tours complets d'attente
                 * au lancement de la partie.
                 */
                leader.AbilityCooldowns[ability.Id] =
                    ability.CooldownTurns + 1;
            }
        }

        private static void Shuffle(
            List<GameCardState> cards)
        {
            for (var i = cards.Count - 1;
                 i > 0;
                 i--)
            {
                var j =
                    Random.Shared.Next(
                        i + 1);

                (cards[i], cards[j]) =
                    (cards[j], cards[i]);
            }
        }

        private static void Finish(
            GameState state,
            string winnerUserId,
            GameEndReason reason)
        {
            state.Status =
                GameMatchStatus.Finished;

            state.WinnerUserId =
                winnerUserId;

            state.EndReason =
                reason;
        }
    }
}
