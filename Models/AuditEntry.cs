namespace DulceAtardecer.Models;

public record AuditEntry(string Action)
{
    public const string UserEntity = "User";
    public const string SessionEntity = "Session";

    public string? UserId { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public object? Details { get; init; }
}
