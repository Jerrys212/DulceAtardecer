using DulceAtardecer.Models;

namespace DulceAtardecer.Repository.IRepository;

public interface ISessionRepository
{
    Task<(Session Session, IssuedToken RefreshToken)> CreateAsync(
        string userId,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<RefreshToken?> FindRefreshTokenAsync(string token, CancellationToken cancellationToken);

    Task<IssuedToken?> TryRotateAsync(RefreshToken current, CancellationToken cancellationToken);

    Task RevokeAsync(Guid sessionId, string reason, CancellationToken cancellationToken);

    Task<int> RevokeAllForUserAsync(
        string userId,
        string reason,
        Guid? exceptSessionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Session>> GetActiveForUserAsync(string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<(Guid Id, DateTimeOffset ExpiresAt)>> GetRevokedUnexpiredAsync(CancellationToken cancellationToken);

    Task<int> DeleteExpiredBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
