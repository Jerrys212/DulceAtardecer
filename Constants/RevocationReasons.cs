namespace DulceAtardecer.Constants;

public static class RevocationReasons
{
    public const string Logout = "logout";
    public const string UserDeactivated = "user_deactivated";
    public const string RoleChanged = "role_changed";
    public const string PasswordReset = "password_reset";
    public const string PasswordChanged = "password_changed";
    public const string RefreshTokenReuse = "refresh_token_reuse";
    public const string RevokedByAdmin = "revoked_by_admin";
}
