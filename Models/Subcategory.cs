namespace DulceAtardecer.Models;

public class Subcategory : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedById { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedById { get; set; }
}
