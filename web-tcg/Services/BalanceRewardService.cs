using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Hubs;

namespace web_tcg.Services
{
    public static class BalanceRewardSettings
    {
        public const decimal RewardAmount = 100m;
        public const decimal MaximumBalance = 10000m;
        public const int RewardIntervalMinutes = 10;
    }

    public class BalanceRewardService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BalanceRewardService> _logger;
        private readonly IHubContext<BalanceHub> _balanceHub;

        public BalanceRewardService(
            IServiceScopeFactory scopeFactory,
            ILogger<BalanceRewardService> logger,
            IHubContext<BalanceHub> balanceHub)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _balanceHub = balanceHub;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "BalanceRewardService démarré.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DistributeRewardsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Erreur lors de la distribution des récompenses.");
                }

                await Task.Delay(
                    TimeSpan.FromMinutes(1),
                    stoppingToken);
            }
        }

        private async Task DistributeRewardsAsync(
            CancellationToken cancellationToken)
        {
            using var scope =
                _scopeFactory.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;

            var users = await context.Users
                .Where(x =>
                    x.LastBalanceRewardAt == null ||
                    x.LastBalanceRewardAt.Value.AddMinutes(
                        BalanceRewardSettings.RewardIntervalMinutes) <= now)
                .ToListAsync(cancellationToken);

            var rewardedUsers =
                new List<(string UserId, decimal Balance)>();

            foreach (var user in users)
            {
                // Première initialisation
                if (user.LastBalanceRewardAt == null)
                {
                    user.LastBalanceRewardAt = now;
                    continue;
                }

                if (user.Balance >=
                    BalanceRewardSettings.MaximumBalance)
                {
                    user.LastBalanceRewardAt = now;
                    continue;
                }

                user.Balance = Math.Min(
                    user.Balance +
                    BalanceRewardSettings.RewardAmount,

                    BalanceRewardSettings.MaximumBalance);

                user.LastBalanceRewardAt = now;

                rewardedUsers.Add(
                    (user.Id, user.Balance));
            }

            if (users.Count == 0)
                return;

            await context.SaveChangesAsync(
                cancellationToken);

            // =====================================================
            // SIGNALR
            // =====================================================

            foreach (var rewardedUser in rewardedUsers)
            {
                await _balanceHub.Clients
                    .User(rewardedUser.UserId)
                    .SendAsync(
                        "BalanceUpdated",
                        new
                        {
                            balance = rewardedUser.Balance,
                            nextRewardAt = now.AddMinutes(
                                BalanceRewardSettings.RewardIntervalMinutes)
                        },
                        cancellationToken);
            }

            _logger.LogInformation(
                "{Count} joueur(s) ont reçu une récompense.",
                rewardedUsers.Count);
        }
    }
}