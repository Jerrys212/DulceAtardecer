namespace DulceAtardecer.Models;

public class Category : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Subcategory> Subcategories { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedById { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedById { get; set; }
}
