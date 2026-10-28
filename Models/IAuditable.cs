namespace DulceAtardecer.Models;

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    string? CreatedById { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    string? UpdatedById { get; set; }
}
