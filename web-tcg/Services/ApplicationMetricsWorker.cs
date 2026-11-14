using Microsoft.EntityFrameworkCore;
using Prometheus;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;

namespace web_tcg.Services;

public sealed class ApplicationMetricsWorker(
    IServiceScopeFactory scopes,
    ILogger<ApplicationMetricsWorker> logger) : BackgroundService
{
    private static readonly Gauge Users = Metrics.CreateGauge("worlds_tcg_users", "Registered human players");
    private static readonly Gauge Matches = Metrics.CreateGauge("worlds_tcg_active_matches", "Active matches");
    private static readonly Gauge Auctions = Metrics.CreateGauge("worlds_tcg_active_auctions", "Active market auctions");
    private static readonly Gauge Cards = Metrics.CreateGauge("worlds_tcg_collection_cards", "Owned card copies");
    private static readonly Gauge LastSuccess = Metrics.CreateGauge(
        "worlds_tcg_business_metrics_last_success_timestamp_seconds", "Last successful business metrics refresh");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Users.Set(await db.Users.CountAsync(x => !x.IsBot, ct));
                Matches.Set(await db.Set<GameMatch>().CountAsync(x => x.Status == GameMatchStatus.InProgress, ct));
                Auctions.Set(await db.MarketAuctions.CountAsync(x => x.Status == AuctionStatus.Active, ct));
                Cards.Set(await db.UserCardCollections.SumAsync(x => (long)x.Quantity, ct));
                LastSuccess.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Actualisation des métriques agrégées indisponible."); }
            if (!await timer.WaitForNextTickAsync(ct)) break;
        }
    }
}
