using DulceAtardecer.Repository.IRepository;

namespace DulceAtardecer.Services;

public class ExpiredSessionCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ExpiredSessionCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval, timeProvider);

        do
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Expired session cleanup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ISessionRepository sessionRepository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();

        DateTimeOffset cutoff = timeProvider.GetUtcNow() - Retention;
        int deleted = await sessionRepository.DeleteExpiredBeforeAsync(cutoff, cancellationToken);

        logger.LogInformation("Deleted {Count} sessions and refresh tokens expired before {Cutoff}", deleted, cutoff);
    }
}
