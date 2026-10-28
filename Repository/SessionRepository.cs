using System.Buffers.Text;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using DulceAtardecer.Data;
using DulceAtardecer.Models;
using DulceAtardecer.Models.Settings;
using DulceAtardecer.Repository.IRepository;
using DulceAtardecer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace DulceAtardecer.Repository;

public class SessionRepository(
    ApplicationDbContext db,
    IRevokedSessionCache revokedSessionCache,
    TimeProvider timeProvider,
    IOptions<SessionSettings> sessionSettings) : ISessionRepository
{
    private const int RefreshTokenBytes = 32;
    private const int UserAgentMaxLength = 512;

    public async Task<(Session Session, IssuedToken RefreshToken)> CreateAsync(
        string userId,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Session session = new()
        {
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now.AddHours(sessionSettings.Value.DurationHours),
            UserAgent = userAgent is { Length: > UserAgentMaxLength } ? userAgent[..UserAgentMaxLength] : userAgent,
            IpAddress = ipAddress,
        };

        (RefreshToken refreshToken, string plainToken) = NewRefreshToken(session.Id, session.ExpiresAt, now);
        session.RefreshTokens.Add(refreshToken);
        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return (session, new IssuedToken(plainToken, refreshToken.ExpiresAt));
    }

    public Task<RefreshToken?> FindRefreshTokenAsync(string token, CancellationToken cancellationToken)
    {
        string tokenHash = Hash(token);
        return db.RefreshTokens
            .Include(t => t.Session)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IssuedToken?> TryRotateAsync(RefreshToken current, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        (RefreshToken next, string plainToken) = NewRefreshToken(current.SessionId, current.ExpiresAt, now);

        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        int updated = await db.RefreshTokens
            .Where(t => t.Id == current.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.UsedAt, now)
                .SetProperty(t => t.ReplacedById, next.Id), cancellationToken);

        if (updated == 0)
        {
            return null;
        }

        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new IssuedToken(plainToken, next.ExpiresAt);
    }

    public Task RevokeAsync(Guid sessionId, string reason, CancellationToken cancellationToken) =>
        RevokeWhereAsync(s => s.Id == sessionId, reason, cancellationToken);

    public Task<int> RevokeAllForUserAsync(
        string userId,
        string reason,
        Guid? exceptSessionId,
        CancellationToken cancellationToken) =>
        RevokeWhereAsync(s => s.UserId == userId && s.Id != exceptSessionId, reason, cancellationToken);

    public async Task<IReadOnlyList<Session>> GetActiveForUserAsync(string userId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return await db.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(Guid Id, DateTimeOffset ExpiresAt)>> GetRevokedUnexpiredAsync(
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var sessions = await db.Sessions
            .Where(s => s.RevokedAt != null && s.ExpiresAt > now)
            .Select(s => new { s.Id, s.ExpiresAt })
            .ToListAsync(cancellationToken);

        return sessions.Select(s => (s.Id, s.ExpiresAt)).ToList();
    }

    public async Task<int> DeleteExpiredBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        int tokens = await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        int sessions = await db.Sessions
            .Where(s => s.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        return tokens + sessions;
    }

    private async Task<int> RevokeWhereAsync(
        Expression<Func<Session, bool>> predicate,
        string reason,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var sessions = await db.Sessions
            .Where(predicate)
            .Where(s => s.RevokedAt == null && s.ExpiresAt > now)
            .Select(s => new { s.Id, s.ExpiresAt })
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return 0;
        }

        List<Guid> sessionIds = sessions.Select(s => s.Id).ToList();

        await db.Sessions
            .Where(s => sessionIds.Contains(s.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.RevokedAt, now)
                .SetProperty(s => s.RevocationReason, reason), cancellationToken);

        await db.RefreshTokens
            .Where(t => sessionIds.Contains(t.SessionId) && t.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ExpiresAt, now), cancellationToken);

        foreach (var session in sessions)
        {
            revokedSessionCache.Add(session.Id, session.ExpiresAt);
        }

        return sessions.Count;
    }

    private static (RefreshToken Entity, string PlainToken) NewRefreshToken(
        Guid sessionId,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        string plainToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        RefreshToken entity = new()
        {
            SessionId = sessionId,
            TokenHash = Hash(plainToken),
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };

        return (entity, plainToken);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
