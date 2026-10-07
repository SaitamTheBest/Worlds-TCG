using System.Text.Json;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Models.Admin;
using web_tcg.Game.Engine;

namespace web_tcg.Services;

public static class CardAbilityEditor
{
    public static CardAbilityInputModel Read(CardAbility ability) => new()
    {
        Id = ability.Id, Type = ability.Type, Name = ability.Name, Description = ability.Description,
        UsageCost = ability.UsageCost, DisplayOrder = ability.DisplayOrder, CooldownTurns = ability.CooldownTurns,
        Effects = ability.Effects.OrderBy(e => e.DisplayOrder).Select(ReadEffect).ToList(),
        Conditions = ability.Conditions.OrderBy(c => c.DisplayOrder).Select(c => new CardConditionInputModel
        { OrientationIds = GameGeography.Ids(GameGeography.Parse(c.ParametersJson),"orientations"), Type = c.Type, Value = c.Value, Negate = c.Negate, CardName = GameGeography.Parse(c.ParametersJson).GetValueOrDefault("cardName") ?? string.Empty,
            Scope = int.TryParse(GameGeography.Parse(c.ParametersJson).GetValueOrDefault("scope"),out var scope) ? scope : 1,
            RegionIds = GameGeography.Ids(GameGeography.Parse(c.ParametersJson),"regions"), CountryIds = GameGeography.Ids(GameGeography.Parse(c.ParametersJson),"countries"), PartyIds = GameGeography.Ids(GameGeography.Parse(c.ParametersJson),"parties"), CardTypes = GameGeography.Ids(GameGeography.Parse(c.ParametersJson),"types") }).ToList()
    };

    public static void Write(CardAbility ability, CardAbilityInputModel input)
    {
        ability.UsageCost = input.Type == AbilityType.Effect ? 0 : input.UsageCost;
        ability.CooldownTurns = input.Type == AbilityType.Trait ? input.CooldownTurns : 0;
        ability.Effects.Clear();
        foreach (var (effect, index) in input.Effects.Select((e, i) => (e, i)))
            ability.Effects.Add(new CardAbilityEffect
            {
                Type = effect.Type, Trigger = effect.Trigger, Target = effect.Target, Value = effect.Value,
                DurationTurns = effect.Type == GameEffectType.Prison ? 1 : effect.DurationTurns, ParametersJson = EffectParameters(effect), DisplayOrder = index
            });
        ability.Conditions.Clear();
        foreach (var (condition, index) in input.Conditions.Select((c, i) => (c, i)))
            ability.Conditions.Add(new CardAbilityCondition
            { Type = condition.Type, Value = condition.Value, Negate = condition.Negate, DisplayOrder = index,
                ParametersJson = JsonSerializer.Serialize(new Dictionary<string,string> { ["orientations"] = string.Join(',',condition.OrientationIds.Distinct()), ["cardName"] = (condition.CardName ?? string.Empty).Trim(), ["scope"] = condition.Scope.ToString(), ["regions"] = string.Join(',',condition.RegionIds.Distinct()), ["countries"] = string.Join(',',condition.CountryIds.Distinct()), ["parties"] = string.Join(',',condition.PartyIds.Distinct()), ["types"] = string.Join(',',condition.CardTypes.Distinct()) }) });
    }

    private static CardEffectInputModel ReadEffect(CardAbilityEffect e)
    {
        var parameters = GameGeography.Parse(e.ParametersJson);
        return new() { Type = e.Type, Trigger = e.Trigger, Target = e.Target, Value = e.Value, DurationTurns = e.DurationTurns, ParametersJson = e.ParametersJson,
            OrientationIds = GameGeography.Ids(parameters,"orientations"), CountOrientationIds = GameGeography.Ids(parameters,"countorientations"),
            RegionIds = GameGeography.Ids(parameters,"regions"), CountryIds = GameGeography.Ids(parameters,"countries"), PartyIds = GameGeography.Ids(parameters,"parties"),
            CountRegionIds = GameGeography.Ids(parameters,"countregions"), CountCountryIds = GameGeography.Ids(parameters,"countcountries"), CountPartyIds = GameGeography.Ids(parameters,"countparties"),
            ScaleByMatchingCharacters = parameters.GetValueOrDefault("scaleByCount") == "true", CountScope = int.TryParse(parameters.GetValueOrDefault("countScope"),out var scope) ? scope : 1 };
    }
    private static string EffectParameters(CardEffectInputModel effect)
    {
        var parameters = GameGeography.Parse(effect.ParametersJson);
        parameters["orientations"] = string.Join(',',effect.OrientationIds.Distinct());
        parameters["countorientations"] = string.Join(',',effect.CountOrientationIds.Distinct());
        parameters["regions"] = string.Join(',',effect.RegionIds.Distinct()); parameters["countries"] = string.Join(',',effect.CountryIds.Distinct());
        parameters["countregions"] = string.Join(',',effect.CountRegionIds.Distinct()); parameters["countcountries"] = string.Join(',',effect.CountCountryIds.Distinct());
        parameters["parties"] = string.Join(',',effect.PartyIds.Distinct()); parameters["countparties"] = string.Join(',',effect.CountPartyIds.Distinct());
        parameters["scaleByCount"] = effect.ScaleByMatchingCharacters ? "true" : "false"; parameters["countScope"] = effect.CountScope.ToString();
        return JsonSerializer.Serialize(parameters);
    }

    public static IEnumerable<string> Validate(CardType cardType, CardAbilityInputModel ability)
    {
        if (cardType == CardType.Secret)
        {
            yield return "Une carte SECRET ne peut pas avoir de capacité.";
            yield break;
        }
        if (!Enum.IsDefined(ability.Type)) yield return "Type de capacité invalide.";
        if (ability.Effects.Count == 0) yield return "Ajoutez au moins une action à la capacité ; la description seule ne produit aucun effet en jeu.";
        if (cardType is CardType.Location or CardType.PoliticalDecision && ability.Type != AbilityType.Effect)
            yield return "Les lieux et décisions utilisent des effets automatiques, pas des compétences ou talents.";
        foreach (var e in ability.Effects)
        {
            if (e.Value < 0 && e.Type is not (GameEffectType.HealthBonus or GameEffectType.DamageBonus))
                yield return "Seuls les bonus/malus de PV et de dégâts acceptent une valeur négative.";
            if (e.ScaleByMatchingCharacters && e.Type is not (GameEffectType.HealthBonus or GameEffectType.DamageBonus))
                yield return "Le calcul par personnage s'applique aux bonus de PV et aux bonus de dégâts fixes.";
            if (ability.Type == AbilityType.Skill && e.Trigger != GameEffectTrigger.OnSkill ||
                ability.Type == AbilityType.Trait && e.Trigger != GameEffectTrigger.OnTalent ||
                ability.Type == AbilityType.Effect && e.Trigger is GameEffectTrigger.OnSkill or GameEffectTrigger.OnTalent)
                yield return "Le déclenchement doit correspondre au type de capacité.";
            if (cardType == CardType.PoliticalDecision && e.Trigger != GameEffectTrigger.OnPlay)
                yield return "Une décision déclenche ses actions quand elle est jouée.";
            if (cardType == CardType.Location && e.Type is GameEffectType.Prison or GameEffectType.Block or GameEffectType.Shield or GameEffectType.Stealth or GameEffectType.ExtraAttack or GameEffectType.Revive)
                yield return "Un lieu ne peut pas donner d'état aux personnages.";
            if (cardType == CardType.Location && e.Type == GameEffectType.Damage)
                yield return "Un lieu produit des bonus et malus, pas d'attaque directe.";
            if (cardType == CardType.Location && e.Trigger is not (GameEffectTrigger.OnPlay or GameEffectTrigger.Passive or GameEffectTrigger.OnTurnStart or GameEffectTrigger.OnTurnEnd))
                yield return "Un lieu déclenche ses bonus à la pose, en passif, au début ou à la fin du tour.";
            if (cardType == CardType.Location && e.Target == GameTargetType.Self && e.Type is GameEffectType.Heal or GameEffectType.DamageMultiplier or GameEffectType.HealthBonus or GameEffectType.DamageBonus)
                yield return "Le lieu n'a ni PV ni attaque : ciblez des personnages ou un Leader pour ce bonus.";
            if (ability.Type == AbilityType.Effect && cardType is CardType.Character or CardType.Leader && e.Type == GameEffectType.Damage && e.Trigger != GameEffectTrigger.OnDeath)
                yield return "Un effet passif ne fait pas de dégâts ; utilisez une compétence, un talent ou Dernier souffle (à la mort).";
            if (e.Type == GameEffectType.Block && (e.Target is GameTargetType.FriendlyLeader or GameTargetType.EnemyLeader or GameTargetType.FriendlyCharacterOrLeader or GameTargetType.EnemyCharacterOrLeader || cardType == CardType.Leader && e.Target == GameTargetType.Self))
                yield return "Un Leader ne peut pas bloquer : ciblez uniquement des personnages.";
            var playerEffect = e.Type is GameEffectType.DrawCards or GameEffectType.DiscardCards or GameEffectType.GainMoney or GameEffectType.LoseMoney or GameEffectType.StealMoney;
            if (playerEffect && e.Target is not (GameTargetType.FriendlyPlayer or GameTargetType.EnemyPlayer))
                yield return "Cette action doit cibler votre joueur ou le joueur adverse.";
            if (e.Type == GameEffectType.StealMoney && e.Target != GameTargetType.EnemyPlayer)
                yield return "Le vol d'énergie cible le joueur adverse.";
            if (e.Type != GameEffectType.DamageMultiplier && e.Value != decimal.Truncate(e.Value))
                yield return "Les dégâts, PV, ressources et nombres d'attaques doivent être des nombres entiers.";
            if (!playerEffect && e.Type != GameEffectType.DestroyLocation && e.Target is not (GameTargetType.Self or GameTargetType.FriendlyCharacter or GameTargetType.FriendlyLeader or GameTargetType.FriendlyCharacterOrLeader or GameTargetType.EnemyCharacter or GameTargetType.EnemyLeader or GameTargetType.EnemyCharacterOrLeader or GameTargetType.AllFriendlyCharacters or GameTargetType.AllEnemyCharacters or GameTargetType.AllCharacters))
                yield return "Cette action doit cibler une carte ou un groupe de personnages.";
            if (e.Type == GameEffectType.DestroyLocation && e.Target != GameTargetType.EnemyLocation)
                yield return "La destruction doit cibler le lieu adverse.";
            if (e.Type == GameEffectType.Revive && (cardType != CardType.Character || e.Target != GameTargetType.Self || e.Trigger != GameEffectTrigger.OnDeath))
                yield return "Second souffle s'applique au personnage lui-même à sa mort.";
            if (e.Type == GameEffectType.DamageMultiplier && e.Value < 1)
                yield return "Le multiplicateur de dégâts doit être au moins égal à 1.";
            if (e.Trigger is GameEffectTrigger.Passive or GameEffectTrigger.OnDeath or GameEffectTrigger.OnTurnStart or GameEffectTrigger.OnTurnEnd && e.Target is GameTargetType.FriendlyCharacter or GameTargetType.FriendlyCharacterOrLeader or GameTargetType.EnemyCharacter or GameTargetType.EnemyCharacterOrLeader)
                yield return "Une action automatique doit cibler cette carte, un Leader, un joueur ou un groupe ; personne ne choisit de cible au déclenchement.";
            if (e.Trigger == GameEffectTrigger.OnDamaged && e.Target is GameTargetType.FriendlyCharacter or GameTargetType.FriendlyCharacterOrLeader)
                yield return "Une réaction aux dégâts doit cibler cette carte, l'attaquant adverse, un Leader, un joueur ou un groupe.";
            if (cardType == CardType.Leader && e.Trigger is GameEffectTrigger.Passive or GameEffectTrigger.OnPlay && e.Target is GameTargetType.FriendlyCharacter or GameTargetType.FriendlyCharacterOrLeader or GameTargetType.EnemyCharacter or GameTargetType.EnemyCharacterOrLeader)
                yield return "Un effet initial du Leader doit utiliser une cible automatique (cette carte, Leader, joueur ou groupe).";
            if (e.DurationTurns > 0 && e.Type is not (GameEffectType.Prison or GameEffectType.Block or GameEffectType.Shield or GameEffectType.Stealth or GameEffectType.ExtraAttack or GameEffectType.DamageMultiplier or GameEffectType.HealthBonus or GameEffectType.DamageBonus))
                yield return "La durée concerne les états et bonus d'attaque ; les dégâts, soins et ressources sont instantanés.";
            if (e.Type == GameEffectType.Prison && (e.Trigger == GameEffectTrigger.Passive || e.DurationTurns != 1))
                yield return "Prison dure exactement un prochain tour et ne peut pas être un effet passif.";
            if (e.Trigger == GameEffectTrigger.Passive && e.DurationTurns != 0)
                yield return "Un effet passif reste actif tant que sa source est sur le terrain : mettez sa durée à 0.";
            if (e.Trigger == GameEffectTrigger.Passive && e.Type is not (GameEffectType.Block or GameEffectType.Shield or GameEffectType.Stealth or GameEffectType.ExtraAttack or GameEffectType.DamageMultiplier or GameEffectType.HealthBonus or GameEffectType.DamageBonus))
                yield return "Le passif concerne les états et bonus continus. Pour les soins, pioches ou ressources, choisissez la pose, le début ou la fin du tour.";
            bool validJson;
            try { using var json = JsonDocument.Parse(e.ParametersJson); validJson = json.RootElement.ValueKind == JsonValueKind.Object; }
            catch (JsonException) { validJson = false; }
            if (!validJson) yield return "Les paramètres de l'action sont invalides.";
        }
        foreach (var condition in ability.Conditions)
        {
            if (condition.Type is GameConditionType.SourcePoliticalOrientation or GameConditionType.TargetPoliticalOrientation && condition.OrientationIds.Count == 0)
                yield return "Choisissez au moins une orientation politique pour cette condition.";
            if (condition.Type is GameConditionType.NamedLeaderOnBoard or GameConditionType.NamedCharacterOnBoard)
            {
                if (string.IsNullOrWhiteSpace(condition.CardName) || condition.CardName.Length > 150)
                    yield return "Indiquez le nom de la carte requise sur le terrain (150 caractères maximum).";
                if (condition.Scope is < 1 or > 3)
                    yield return "Choisissez le camp de la carte requise : allié, adverse ou les deux.";
            }
            if (condition.Type is GameConditionType.RevealHandCards or GameConditionType.DiscardHandCards)
            {
                if (condition.Negate || condition.Value < 1 || condition.Value > 40 || condition.Value != decimal.Truncate(condition.Value))
                    yield return "Dévoiler/défausser demande de 1 à 40 cartes et ne peut pas être inversé.";
                if (ability.Effects.Any(e => e.Trigger is not (GameEffectTrigger.OnPlay or GameEffectTrigger.OnSkill or GameEffectTrigger.OnTalent)))
                    yield return "Un coût de main nécessite une activation manuelle ou la pose d'une carte ; il ne peut pas accompagner un effet automatique ou passif.";
                if (cardType == CardType.Leader && ability.Effects.Any(e=>e.Trigger==GameEffectTrigger.OnPlay))
                    yield return "L'effet initial du Leader ne peut pas demander un choix de cartes de main.";
                if (condition.CardTypes.Any(t => t is < 2 or > 5)) yield return "Types de cartes de main invalides.";
            }
            if (condition.Type is GameConditionType.SourceParty or GameConditionType.TargetParty && condition.PartyIds.Count == 0) yield return "Choisissez au moins un parti pour cette condition.";
            if (condition.Type is GameConditionType.SourceRegion or GameConditionType.TargetRegion && condition.RegionIds.Count == 0) yield return "Choisissez au moins une région pour cette condition.";
            if (condition.Type is GameConditionType.SourceCountry or GameConditionType.TargetCountry && condition.CountryIds.Count == 0) yield return "Choisissez au moins un pays pour cette condition.";
        }
        var geographicIds = ability.Effects.SelectMany(e => e.RegionIds.Concat(e.CountryIds).Concat(e.CountRegionIds).Concat(e.CountCountryIds).Concat(e.PartyIds).Concat(e.CountPartyIds)).Concat(ability.Conditions.SelectMany(c => c.RegionIds.Concat(c.CountryIds).Concat(c.PartyIds))).ToList();
        if (ability.Effects.SelectMany(e=>e.OrientationIds.Concat(e.CountOrientationIds)).Concat(ability.Conditions.SelectMany(c=>c.OrientationIds)).Any(id=>!Enum.IsDefined(typeof(PoliticalOrientation),id)))
            yield return "Orientation politique invalide.";
        if (geographicIds.Count > 256 || geographicIds.Any(id => id <= 0)) yield return "Sélection géographique invalide ou trop grande.";
        var chosenTargets = ability.Effects.Where(e => e.Target is GameTargetType.FriendlyCharacter or GameTargetType.FriendlyCharacterOrLeader or GameTargetType.EnemyCharacter or GameTargetType.EnemyLeader or GameTargetType.EnemyCharacterOrLeader).Select(e => e.Target).ToList();
        if (chosenTargets.Count > 0 && !new[] { GameTargetType.FriendlyCharacter, GameTargetType.FriendlyLeader, GameTargetType.EnemyCharacter, GameTargetType.EnemyLeader }
            .Any(candidate => chosenTargets.All(t => t == candidate || t == GameTargetType.FriendlyCharacterOrLeader && candidate is GameTargetType.FriendlyCharacter or GameTargetType.FriendlyLeader || t == GameTargetType.EnemyCharacterOrLeader && candidate is GameTargetType.EnemyCharacter or GameTargetType.EnemyLeader)))
            yield return "Les actions ciblées d'une capacité partagent une seule cible choisie. Pour d'autres bénéficiaires, utilisez Cette carte, un groupe ou un joueur.";
    }
}
