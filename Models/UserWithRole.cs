namespace DulceAtardecer.Models;

public record UserWithRole(ApplicationUser User, string Role);

public record AuthResult(UserWithRole User, IssuedToken AccessToken, IssuedToken RefreshToken);

public record RefreshResult(IssuedToken AccessToken, IssuedToken? RefreshToken);
