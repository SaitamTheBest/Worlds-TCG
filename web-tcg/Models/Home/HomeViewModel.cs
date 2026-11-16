namespace web_tcg.Models.Home
{
    public sealed class HomeDeckOption
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public sealed class HomeViewModel
    {
        public int AvailableCards { get; init; }
        public int BaseCards { get; init; }
        public int Variants { get; init; }
        public int RegisteredPlayers { get; init; }
        public int CardSets { get; init; }
        public int BoosterOpenings { get; init; }
        public int LeaderCards { get; init; }
        public int CharacterCards { get; init; }
        public int PoliticalDecisionCards { get; init; }
        public int LocationCards { get; init; }
        public int SecretCards { get; init; }
        public int UserUniqueCards { get; init; }
        public int UserTotalCards { get; init; }
        public double CollectionProgress { get; init; }

        public IReadOnlyList<HomeDeckOption> UserDecks { get; init; } =
            Array.Empty<HomeDeckOption>();
    }
}
