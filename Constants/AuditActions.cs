namespace DulceAtardecer.Constants;

public static class AuditActions
{
    public const string LoginSucceeded = "login.succeeded";
    public const string LoginFailed = "login.failed";
    public const string Logout = "logout";
    public const string PasswordChanged = "password.changed";
    public const string PasswordReset = "password.reset";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserRoleChanged = "user.role_changed";
    public const string UserDeactivated = "user.deactivated";
    public const string UserReactivated = "user.reactivated";
    public const string SessionsRevoked = "sessions.revoked";
    public const string RefreshTokenReuse = "security.refresh_token_reuse";
}
