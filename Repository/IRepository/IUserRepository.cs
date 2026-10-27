using DulceAtardecer.Models;

namespace DulceAtardecer.Repository.IRepository;

public interface IUserRepository
{
    Task<AuthResult> LoginAsync(
        string username,
        string password,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<UserWithRole> GetCurrentAsync(string userId, CancellationToken cancellationToken);

    Task ChangePasswordAsync(
        string userId,
        Guid currentSessionId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);

    Task<PagedResult<UserWithRole>> GetPagedAsync(
        bool? isActive,
        string? role,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<UserWithRole> GetByIdAsync(string userId, CancellationToken cancellationToken);

    Task<(UserWithRole User, string TemporaryPassword)> CreateAsync(
        string username,
        string fullName,
        string role,
        CancellationToken cancellationToken);

    Task<UserWithRole> UpdateAsync(
        string userId,
        string fullName,
        string role,
        string actorId,
        CancellationToken cancellationToken);

    Task DeactivateAsync(string userId, string actorId, CancellationToken cancellationToken);

    Task<string> ReactivateAsync(string userId, CancellationToken cancellationToken);

    Task<string> ResetPasswordAsync(string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Session>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken);

    Task RevokeSessionsAsync(string userId, CancellationToken cancellationToken);
}
