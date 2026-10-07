namespace web_tcg.Data.Enums
{
    public enum GameMatchStatus
    {
        Starting = 1,
        InProgress = 2,
        Finished = 3
    }

    public enum GameEndReason
    {
        LeaderDefeated = 1,
        Surrender = 2,
        Inactivity = 3,
        TimeExpired = 4
    }

    public enum GameCardZone
    {
        Deck = 1,
        Hand = 2,
        Leader = 3,
        CharacterBoard = 4,
        Location = 5,
        Graveyard = 6
    }

    public enum GameEffectType
    {
        Damage = 1,
        Heal = 2,
        DrawCards = 3,

        GainMoney = 10,
        LoseMoney = 11,
        StealMoney = 12,

        Block = 20,
        Shield = 21,
        Stealth = 22,
        Prison = 23,

        ExtraAttack = 30,
        DamageMultiplier = 31,
        HealthBonus = 32,
        DamageBonus = 33,

        Revive = 40,

        DiscardCards = 50,

        DestroyLocation = 60
    }

    public enum GameEffectTrigger
    {
        OnPlay = 1,
        OnSkill = 2,
        OnTalent = 3,
        Passive = 4,
        OnDeath = 5,
        OnTurnStart = 6,
        OnTurnEnd = 7,
        OnDamaged = 8
    }

    public enum GameTargetType
    {
        None = 0,
        Self = 1,

        FriendlyCharacter = 10,
        FriendlyLeader = 11,
        FriendlyCharacterOrLeader = 12,

        EnemyCharacter = 20,
        EnemyLeader = 21,
        EnemyCharacterOrLeader = 22,

        FriendlyLocation = 30,
        EnemyLocation = 31,

        FriendlyPlayer = 40,
        EnemyPlayer = 41,

        AllFriendlyCharacters = 50,
        AllEnemyCharacters = 51,
        AllCharacters = 52
    }

    public enum GameConditionType
    {
        None = 0,
        SourceHealthBelow = 1,
        SourceHealthAbove = 2,
        TargetHealthBelow = 10,
        TargetHealthAbove = 11,
        PlayerMoneyAtLeast = 20,
        OpponentHasCharacter = 30,
        OpponentHasLocation = 31,
        FriendlyCharacterCountAtLeast = 40,
        EnemyCharacterCountAtLeast = 41,
        SourceRegion = 50,
        SourceCountry = 51,
        TargetRegion = 52,
        TargetCountry = 53,
        SourceParty = 54,
        TargetParty = 55,
        SourcePoliticalOrientation = 56,
        TargetPoliticalOrientation = 57,
        MatchingFriendlyCharactersAtLeast = 60,
        MatchingEnemyCharactersAtLeast = 61,
        MatchingCharactersAtLeast = 62,
        RevealHandCards = 70,
        DiscardHandCards = 71,
        NamedLeaderOnBoard = 80,
        NamedCharacterOnBoard = 81
    }

    public enum GameComparisonOperator
    {
        Equal = 1,
        NotEqual = 2,
        GreaterThan = 3,
        GreaterThanOrEqual = 4,
        LessThan = 5,
        LessThanOrEqual = 6
    }
}
