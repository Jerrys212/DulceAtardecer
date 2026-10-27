using System.Security.Claims;
using System.Security.Cryptography;
using DulceAtardecer.Constants;
using DulceAtardecer.Data;
using DulceAtardecer.Models;
using DulceAtardecer.Models.Settings;
using DulceAtardecer.Repository.IRepository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DulceAtardecer.Repository;

public class UserRepository(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ISessionRepository sessionRepository,
    IAuditRepository auditRepository,
    IOptions<ApiSettings> apiSettings,
    TimeProvider timeProvider) : IUserRepository
{
    private const string InvalidCredentialsMessage = "Usuario o contraseña incorrectos";
    private const string TemporaryPasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
    private const int TemporaryPasswordLength = 8;
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
            throw await LoginFailedAsync(username, "unknown_user", cancellationToken);
        }

        if (!user.IsActive)
        {
            throw await LoginFailedAsync(username, "inactive", cancellationToken);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw await LoginFailedAsync(username, "locked_out", cancellationToken);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            string reason = await userManager.IsLockedOutAsync(user) ? "bad_password_locked_out" : "bad_password";
            throw await LoginFailedAsync(username, reason, cancellationToken);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        string role = await GetRoleAsync(user);
        (Session session, IssuedToken refreshToken) =
            await sessionRepository.CreateAsync(user.Id, userAgent, ipAddress, cancellationToken);

        await auditRepository.LogAsync(new AuditEntry(AuditActions.LoginSucceeded)
        {
            UserId = user.Id,
            EntityType = AuditEntry.SessionEntity,
            EntityId = session.Id.ToString(),
        }, cancellationToken);

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
                await auditRepository.LogAsync(new AuditEntry(AuditActions.RefreshTokenReuse)
                {
                    UserId = session.UserId,
                    EntityType = AuditEntry.SessionEntity,
                    EntityId = session.Id.ToString(),
                    Details = new { refreshTokenId = stored.Id, usedAt },
                }, cancellationToken);
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

    public async Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await sessionRepository.RevokeAsync(sessionId, RevocationReasons.Logout, cancellationToken);
        await auditRepository.LogAsync(new AuditEntry(AuditActions.Logout)
        {
            EntityType = AuditEntry.SessionEntity,
            EntityId = sessionId.ToString(),
        }, cancellationToken);
    }

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

        int revokedSessions = await sessionRepository.RevokeAllForUserAsync(
            user.Id,
            RevocationReasons.PasswordChanged,
            currentSessionId,
            cancellationToken);

        await auditRepository.LogAsync(new AuditEntry(AuditActions.PasswordChanged)
        {
            EntityType = AuditEntry.UserEntity,
            EntityId = user.Id,
            Details = new { revokedSessions },
        }, cancellationToken);
    }

    public async Task<PagedResult<UserWithRole>> GetPagedAsync(
        bool? isActive,
        string? role,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join identityRole in db.Roles on userRole.RoleId equals identityRole.Id
            select new { User = user, Role = identityRole.Name ?? string.Empty };

        if (isActive is not null)
        {
            query = query.Where(x => x.User.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(x => x.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(x => (x.User.UserName ?? string.Empty).Contains(term) || x.User.FullName.Contains(term));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.User.FullName)
            .ThenBy(x => x.User.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserWithRole>(
            items.Select(x => new UserWithRole(x.User, x.Role)).ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<UserWithRole> GetByIdAsync(string userId, CancellationToken cancellationToken)
    {
        ApplicationUser user = await FindOrThrowAsync(userId);
        return new UserWithRole(user, await GetRoleAsync(user));
    }

    public async Task<(UserWithRole User, string TemporaryPassword)> CreateAsync(
        string username,
        string fullName,
        string role,
        CancellationToken cancellationToken)
    {
        ApplicationUser user = new() { UserName = username, FullName = fullName.Trim(), MustChangePassword = true };
        string temporaryPassword = GenerateTemporaryPassword();
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, temporaryPassword);

        string roleId = await db.Roles
            .Where(r => r.Name == role)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleId });

        EnsureSucceeded(await userManager.CreateAsync(user), "username");

        await auditRepository.LogAsync(new AuditEntry(AuditActions.UserCreated)
        {
            EntityType = AuditEntry.UserEntity,
            EntityId = user.Id,
            Details = new { username, role },
        }, cancellationToken);

        return (new UserWithRole(user, role), temporaryPassword);
    }

    public async Task<UserWithRole> UpdateAsync(
        string userId,
        string fullName,
        string role,
        string actorId,
        CancellationToken cancellationToken)
    {
        ApplicationUser user = await FindOrThrowAsync(userId);
        string currentRole = await GetRoleAsync(user);
        bool roleChanged = currentRole != role;

        if (roleChanged)
        {
            if (user.Id == actorId)
            {
                throw new ApiException(StatusCodes.Status400BadRequest, "No puedes cambiar tu propio rol");
            }

            if (currentRole == RoleNames.Admin && user.IsActive)
            {
                await EnsureAnotherActiveAdminAsync(user.Id, cancellationToken);
            }
        }

        string previousFullName = user.FullName;
        user.FullName = fullName.Trim();

        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        EnsureSucceeded(await userManager.UpdateAsync(user), "fullName");
        if (roleChanged)
        {
            EnsureSucceeded(await userManager.RemoveFromRoleAsync(user, currentRole), "role");
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role), "role");
        }

        await transaction.CommitAsync(cancellationToken);

        if (roleChanged)
        {
            await sessionRepository.RevokeAllForUserAsync(user.Id, RevocationReasons.RoleChanged, null, cancellationToken);
        }

        await auditRepository.LogAsync(new AuditEntry(roleChanged ? AuditActions.UserRoleChanged : AuditActions.UserUpdated)
        {
            EntityType = AuditEntry.UserEntity,
            EntityId = user.Id,
            Details = new { previousFullName, fullName = user.FullName, previousRole = currentRole, role },
        }, cancellationToken);

        return new UserWithRole(user, role);
    }

    public async Task DeactivateAsync(string userId, string actorId, CancellationToken cancellationToken)
    {
        if (userId == actorId)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "No puedes desactivarte a ti mismo");
        }

        ApplicationUser user = await FindOrThrowAsync(userId);
        if (!user.IsActive)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "El usuario ya está desactivado");
        }

        if (await GetRoleAsync(user) == RoleNames.Admin)
        {
            await EnsureAnotherActiveAdminAsync(user.Id, cancellationToken);
        }

        user.IsActive = false;
        user.DeactivatedAt = timeProvider.GetUtcNow();
        user.DeactivatedById = actorId;
        EnsureSucceeded(await userManager.UpdateAsync(user), "id");

        int revokedSessions = await sessionRepository.RevokeAllForUserAsync(
            user.Id,
            RevocationReasons.UserDeactivated,
            null,
            cancellationToken);

        await auditRepository.LogAsync(new AuditEntry(AuditActions.UserDeactivated)
        {
            EntityType = AuditEntry.UserEntity,
            EntityId = user.Id,
            Details = new { revokedSessions },
        }, cancellationToken);
    }

    public async Task<string> ReactivateAsync(string userId, CancellationToken cancellationToken)
    {
        ApplicationUser user = await FindOrThrowAsync(userId);
        if (user.IsActive)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "El usuario ya está activo");
        }

        user.IsActive = true;
        user.DeactivatedAt = null;
        user.DeactivatedById = null;

        string temporaryPassword = await SetTemporaryPasswordAsync(user, cancellationToken);

        await auditRepository.LogAsync(new AuditEntry(AuditActions.UserReactivated)
        {
            EntityType = AuditEntry.UserEntity,
            EntityId = user.Id,
        }, cancellationToken);

        return temporaryPassword;
    }

    public async Task<string> ResetPasswordAsync(string userId, CancellationToken cancellationToken)
    {
        ApplicationUser user = await FindOrThrowAsync(userId);
        string temporaryPassword = await SetTemporaryPasswordAsync(user, cancellationToken);

        await auditRepository.LogAsync(new AuditEntry(AuditActions.PasswordReset)
        {
            EntityType = AuditEntry.UserEntity,
            EntityId = user.Id,
        }, cancellationToken);

        return temporaryPassword;
    }

    public async Task<IReadOnlyList<Session>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken)
    {
        ApplicationUser user = await FindOrThrowAsync(userId);
        return await sessionRepository.GetActiveForUserAsync(user.Id, cancellationToken);
    }

    public async Task RevokeSessionsAsync(string userId, CancellationToken cancellationToken)
    {
        ApplicationUser user = await FindOrThrowAsync(userId);
        int revokedSessions = await sessionRepository.RevokeAllForUserAsync(
            user.Id,
            RevocationReasons.RevokedByAdmin,
            null,
            cancellationToken);

        await auditRepository.LogAsync(new AuditEntry(AuditActions.SessionsRevoked)
        {
            EntityType = AuditEntry.UserEntity,
            EntityId = user.Id,
            Details = new { revokedSessions },
        }, cancellationToken);
    }

    private async Task<string> SetTemporaryPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        string temporaryPassword = GenerateTemporaryPassword();
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, temporaryPassword);
        user.MustChangePassword = true;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user), "id");
        await sessionRepository.RevokeAllForUserAsync(user.Id, RevocationReasons.PasswordReset, null, cancellationToken);

        return temporaryPassword;
    }

    private async Task EnsureAnotherActiveAdminAsync(string excludedUserId, CancellationToken cancellationToken)
    {
        bool anotherAdminExists = await (
            from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join identityRole in db.Roles on userRole.RoleId equals identityRole.Id
            where identityRole.Name == RoleNames.Admin && user.IsActive && user.Id != excludedUserId
            select user.Id).AnyAsync(cancellationToken);

        if (!anotherAdminExists)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Debe existir al menos un administrador activo");
        }
    }

    private async Task<ApplicationUser> FindOrThrowAsync(string userId) =>
        await userManager.FindByIdAsync(userId)
        ?? throw new ApiException(StatusCodes.Status404NotFound, "Usuario no encontrado");

    private static void EnsureSucceeded(IdentityResult result, string field)
    {
        if (!result.Succeeded)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "Hay errores de validación",
                new Dictionary<string, string[]> { [field] = result.Errors.Select(e => e.Description).ToArray() });
        }
    }

    private static string GenerateTemporaryPassword() =>
        RandomNumberGenerator.GetString(TemporaryPasswordAlphabet, TemporaryPasswordLength);

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

    private async Task<ApiException> LoginFailedAsync(string username, string reason, CancellationToken cancellationToken)
    {
        await auditRepository.LogAsync(new AuditEntry(AuditActions.LoginFailed)
        {
            Details = new { username, reason },
        }, cancellationToken);

        return new ApiException(StatusCodes.Status401Unauthorized, InvalidCredentialsMessage);
    }
}
