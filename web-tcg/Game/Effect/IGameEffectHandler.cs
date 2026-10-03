using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Game.Effects
{
    public interface IGameEffectHandler
    {
        GameEffectType EffectType { get; }

        Task ExecuteAsync(
            GameEffectContext context,
            GameEffectDefinition effect,
            CancellationToken cancellationToken = default);
    }

    public sealed class GameEffectContext
    {
        public GameState State { get; init; } =
            null!;

        public GamePlayerState Player { get; init; } =
            null!;

        public GameCardState? Source { get; init; }

        public GameCardState? Target { get; set; }

        public Guid? RequestedTargetId { get; init; }
        public IReadOnlyList<GameConditionDefinition> TargetConditions { get; init; } = Array.Empty<GameConditionDefinition>();

        public List<string> Logs { get; } =
            new();

        public GamePlayerState Opponent =>
            State.GetOpponent(Player.UserId);
    }
}
