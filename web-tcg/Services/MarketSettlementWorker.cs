namespace web_tcg.Services;

public sealed class MarketSettlementWorker(IServiceScopeFactory scopes, ILogger<MarketSettlementWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<MarketService>().CloseExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Échec de clôture des enchères, nouvelle tentative au prochain passage."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
