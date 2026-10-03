namespace web_tcg.Game.Engine
{
    public sealed class GameRuleException : Exception
    {
        public GameRuleException(string message)
            : base(message)
        {
        }
    }
}