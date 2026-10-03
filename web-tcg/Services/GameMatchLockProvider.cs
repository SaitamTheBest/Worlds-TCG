using System.Collections.Concurrent;

namespace web_tcg.Services
{
    public sealed class GameMatchLockProvider
    {
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks =
            new();

        public SemaphoreSlim Get(
            Guid matchId)
        {
            return _locks.GetOrAdd(
                matchId,
                _ => new SemaphoreSlim(1, 1));
        }
    }
}
