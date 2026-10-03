using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Game.Runtime;

namespace web_tcg.Services
{
    public sealed class GameStateRepository
    {
        private readonly ApplicationDbContext _context;

        private static readonly JsonSerializerOptions JsonOptions =
            new()
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,

                Converters =
                {
                    new JsonStringEnumConverter()
                }
            };

        public GameStateRepository(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<GameState?> LoadAsync(
            Guid matchId,
            CancellationToken cancellationToken = default)
        {
            var json =
                await _context.Set<GameMatch>()
                    .AsNoTracking()
                    .Where(x => x.Id == matchId)
                    .Select(x => x.StateJson)
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<GameState>(
                json,
                JsonOptions);
        }

        public async Task SaveAsync(
            GameState state,
            CancellationToken cancellationToken = default)
        {
            var match =
                await _context.Set<GameMatch>()
                    .FirstAsync(
                        x =>
                            x.Id ==
                            state.MatchId,
                        cancellationToken);

            match.StateJson =
                JsonSerializer.Serialize(
                    state,
                    JsonOptions);

            match.StateVersion =
                state.Version;

            match.Status =
                state.Status;

            match.WinnerUserId =
                state.WinnerUserId;

            match.EndReason =
                state.EndReason;

            if (state.Status ==
                    GameMatchStatus.InProgress &&
                !match.StartedAt.HasValue)
            {
                match.StartedAt =
                    DateTime.UtcNow;
            }

            if (state.Status ==
                    GameMatchStatus.Finished &&
                !match.FinishedAt.HasValue)
            {
                match.FinishedAt =
                    DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(
                cancellationToken);
        }

        public async Task AddLogsAsync(
            Guid matchId,
            IEnumerable<string> messages,
            CancellationToken cancellationToken = default)
        {
            foreach (var message in messages)
            {
                _context.Set<GameLogEntry>().Add(
                    new GameLogEntry
                    {
                        GameMatchId =
                            matchId,

                        Message =
                            message,

                        CreatedAt =
                            DateTime.UtcNow
                    });
            }

            await _context.SaveChangesAsync(
                cancellationToken);
        }

        public Task<List<Guid>> GetActiveMatchIdsAsync(
            CancellationToken cancellationToken = default)
        {
            return _context.Set<GameMatch>()
                .AsNoTracking()
                .Where(x =>
                    x.Status ==
                    GameMatchStatus.InProgress)
                .Select(x => x.Id)
                .ToListAsync(
                    cancellationToken);
        }
    }
}