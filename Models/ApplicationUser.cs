using Microsoft.AspNetCore.Identity;

namespace DulceAtardecer.Models;

public class ApplicationUser : IdentityUser, IAuditable
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7().ToString();
    }

    public required string FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
    public string? DeactivatedById { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedById { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedById { get; set; }
}
