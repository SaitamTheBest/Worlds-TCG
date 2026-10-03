namespace web_tcg.Services;

/// <summary>File FIFO protégée : annulation et appariement partagent le même verrou.</summary>
public sealed class MatchmakingService
{
    public sealed class QueueEntry
    {
        public required string UserId { get; init; }
        public required Guid DeckId { get; init; }
        public string? ConnectionId { get; init; }
    }
    private readonly List<QueueEntry> _queue = new();
    private readonly object _sync = new();
    public int QueueCount { get { lock (_sync) return _queue.Count; } }
    public bool IsQueued(string userId) { lock (_sync) return _queue.Any(entry => entry.UserId == userId); }
    public void Leave(string userId, string? connectionId = null)
    {
        lock (_sync) _queue.RemoveAll(entry => entry.UserId == userId && (connectionId == null || entry.ConnectionId == connectionId));
    }
    public Task<QueueEntry?> TryEnqueueAndMatchAsync(string userId, Guid deckId, CancellationToken cancellationToken = default, string? connectionId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _queue.RemoveAll(entry => entry.UserId == userId);
            if (_queue.Count > 0)
            {
                var opponent = _queue[0];
                _queue.RemoveAt(0);
                return Task.FromResult<QueueEntry?>(opponent);
            }
            _queue.Add(new QueueEntry { UserId = userId, DeckId = deckId, ConnectionId = connectionId });
            return Task.FromResult<QueueEntry?>(null);
        }
    }
}
