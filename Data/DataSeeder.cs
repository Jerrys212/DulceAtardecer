using DulceAtardecer.Constants;
using DulceAtardecer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DulceAtardecer.Data;

public static class DataSeeder
{
    private const int MinimumPasswordLength = 10;

    public static void Seed(DbContext context, IConfiguration configuration)
    {
        ApplicationDbContext db = (ApplicationDbContext)context;
        string adminRoleId = db.Roles.Where(r => r.Name == RoleNames.Admin).Select(r => r.Id).Single();

        if (db.UserRoles.Any(ur => ur.RoleId == adminRoleId))
        {
            return;
        }

        AddInitialAdmin(db, adminRoleId, configuration);
        db.SaveChanges();
    }

    public static async Task SeedAsync(DbContext context, IConfiguration configuration, CancellationToken cancellationToken)
    {
        ApplicationDbContext db = (ApplicationDbContext)context;
        string adminRoleId = await db.Roles
            .Where(r => r.Name == RoleNames.Admin)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);

        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == adminRoleId, cancellationToken))
        {
            return;
        }

        AddInitialAdmin(db, adminRoleId, configuration);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AddInitialAdmin(ApplicationDbContext db, string adminRoleId, IConfiguration configuration)
    {
        string username = configuration["InitialAdmin:Username"] ?? string.Empty;
        string password = configuration["InitialAdmin:Password"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username)
            || password.Length < MinimumPasswordLength
            || string.Equals(username, password, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "InitialAdmin:Username and InitialAdmin:Password are required to seed the first admin; " +
                $"the password must have at least {MinimumPasswordLength} characters and differ from the username.");
        }

        ApplicationUser admin = new()
        {
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            FullName = "Administrador",
            MustChangePassword = true,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        admin.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(admin, password);

        db.Users.Add(admin);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = admin.Id, RoleId = adminRoleId });
    }
}
