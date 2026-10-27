using DulceAtardecer.Repository.IRepository;

namespace DulceAtardecer.Services;

public class RevokedSessionCacheLoader(
    IServiceScopeFactory scopeFactory,
    IRevokedSessionCache revokedSessionCache,
    ILogger<RevokedSessionCacheLoader> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ISessionRepository sessionRepository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();

        IReadOnlyList<(Guid Id, DateTimeOffset ExpiresAt)> sessions =
            await sessionRepository.GetRevokedUnexpiredAsync(cancellationToken);

        foreach ((Guid id, DateTimeOffset expiresAt) in sessions)
        {
            revokedSessionCache.Add(id, expiresAt);
        }

        logger.LogInformation("Loaded {Count} revoked sessions into cache", sessions.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
