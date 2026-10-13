namespace web_tcg.Services;

public sealed class FriendDuelExpiryWorker(IServiceScopeFactory scopes,ILogger<FriendDuelExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<FriendDuelService>().ExpireAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex,"Expiration des invitations de duel temporairement indisponible."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
