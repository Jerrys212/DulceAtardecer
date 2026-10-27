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
}
