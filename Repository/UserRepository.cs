using System.Security.Claims;
using DulceAtardecer.Constants;
using DulceAtardecer.Models;
using DulceAtardecer.Models.Settings;
using DulceAtardecer.Repository.IRepository;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DulceAtardecer.Repository;

public class UserRepository(
    UserManager<ApplicationUser> userManager,
    ISessionRepository sessionRepository,
    IOptions<ApiSettings> apiSettings,
    TimeProvider timeProvider) : IUserRepository
{
    private const string InvalidCredentialsMessage = "Usuario o contraseña incorrectos";
    private static readonly TimeSpan RefreshReuseGracePeriod = TimeSpan.FromSeconds(10);
    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser { FullName = string.Empty }, Guid.NewGuid().ToString()));

    public async Task<AuthResult> LoginAsync(
        string username,
        string password,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByNameAsync(username);

        if (user is null)
        {
            userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser { FullName = string.Empty }, DummyPasswordHash.Value, password);
            throw InvalidCredentials();
        }

        if (!user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            throw InvalidCredentials();
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            throw InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        string role = await GetRoleAsync(user);
        (Session session, IssuedToken refreshToken) =
            await sessionRepository.CreateAsync(user.Id, userAgent, ipAddress, cancellationToken);

        return new AuthResult(new UserWithRole(user, role), CreateAccessToken(user, role, session.Id), refreshToken);
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        RefreshToken? stored = await sessionRepository.FindRefreshTokenAsync(refreshToken, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();

        if (stored?.Session is not { } session
            || session.RevokedAt is not null
            || session.ExpiresAt <= now
            || stored.ExpiresAt <= now)
        {
            throw new ApiException(StatusCodes.Status401Unauthorized);
        }

        IssuedToken? rotatedToken = null;
        if (stored.UsedAt is { } usedAt)
        {
            if (now - usedAt > RefreshReuseGracePeriod)
            {
                await sessionRepository.RevokeAsync(session.Id, RevocationReasons.RefreshTokenReuse, cancellationToken);
                throw new ApiException(StatusCodes.Status401Unauthorized);
            }
        }
        else
        {
            rotatedToken = await sessionRepository.TryRotateAsync(stored, cancellationToken);
        }

        ApplicationUser? user = await userManager.FindByIdAsync(session.UserId);
        if (user is not { IsActive: true })
        {
            throw new ApiException(StatusCodes.Status401Unauthorized);
        }

        string role = await GetRoleAsync(user);
        return new RefreshResult(CreateAccessToken(user, role, session.Id), rotatedToken);
    }

    public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken) =>
        sessionRepository.RevokeAsync(sessionId, RevocationReasons.Logout, cancellationToken);

    public async Task<UserWithRole> GetCurrentAsync(string userId, CancellationToken cancellationToken)
    {
        ApplicationUser user = await userManager.FindByIdAsync(userId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized);

        return new UserWithRole(user, await GetRoleAsync(user));
    }

    public async Task ChangePasswordAsync(
        string userId,
        Guid currentSessionId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        ApplicationUser user = await userManager.FindByIdAsync(userId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized);

        if (!await userManager.CheckPasswordAsync(user, currentPassword))
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "Hay errores de validación",
                new Dictionary<string, string[]> { ["currentPassword"] = ["La contraseña actual es incorrecta"] });
        }

        user.MustChangePassword = false;
        IdentityResult result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "Hay errores de validación",
                new Dictionary<string, string[]> { ["newPassword"] = result.Errors.Select(e => e.Description).ToArray() });
        }

        await sessionRepository.RevokeAllForUserAsync(
            user.Id,
            RevocationReasons.PasswordChanged,
            currentSessionId,
            cancellationToken);
    }

    private async Task<string> GetRoleAsync(ApplicationUser user) =>
        (await userManager.GetRolesAsync(user)).FirstOrDefault()
        ?? throw new InvalidOperationException($"User {user.Id} has no role assigned.");

    private IssuedToken CreateAccessToken(ApplicationUser user, string role, Guid sessionId)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now.AddMinutes(apiSettings.Value.AccessTokenMinutes);

        List<Claim> claims =
        [
            new(AppClaimTypes.Subject, user.Id),
            new(AppClaimTypes.SessionId, sessionId.ToString()),
            new(AppClaimTypes.Name, user.UserName ?? string.Empty),
            new(AppClaimTypes.Role, role),
            new(AppClaimTypes.TokenId, Guid.CreateVersion7().ToString()),
        ];

        if (user.MustChangePassword)
        {
            claims.Add(new Claim(AppClaimTypes.MustChangePassword, "true"));
        }

        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = apiSettings.Value.Issuer,
            Audience = apiSettings.Value.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                apiSettings.Value.CreateSigningKey(),
                SecurityAlgorithms.HmacSha256),
        });

        return new IssuedToken(token, expiresAt);
    }

    private static ApiException InvalidCredentials() =>
        new(StatusCodes.Status401Unauthorized, InvalidCredentialsMessage);
}
