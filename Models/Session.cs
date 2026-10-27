namespace DulceAtardecer.Models;

public class Session
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
