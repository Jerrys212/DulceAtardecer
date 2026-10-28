namespace DulceAtardecer.Models;

public record IssuedToken(string Token, DateTimeOffset ExpiresAt);
