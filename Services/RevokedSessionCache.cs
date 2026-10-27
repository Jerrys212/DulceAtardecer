using Microsoft.Extensions.Caching.Memory;

namespace DulceAtardecer.Services;

public interface IRevokedSessionCache
{
    void Add(Guid sessionId, DateTimeOffset sessionExpiresAt);
    bool IsRevoked(Guid sessionId);
}

public class RevokedSessionCache(IMemoryCache cache) : IRevokedSessionCache
{
    private const string KeyPrefix = "revoked-session:";

    public void Add(Guid sessionId, DateTimeOffset sessionExpiresAt) =>
        cache.Set(KeyPrefix + sessionId, true, sessionExpiresAt);

    public bool IsRevoked(Guid sessionId) => cache.TryGetValue(KeyPrefix + sessionId, out _);
}
