using DulceAtardecer.Constants;
using DulceAtardecer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DulceAtardecer.Data;

public static class DataSeeder
{
    private const int MinimumPasswordLength = 10;
    private const string SeedDemoSalesKey = "DemoData:SeedSales";

    public static void Seed(DbContext context, IConfiguration configuration)
    {
        ApplicationDbContext db = (ApplicationDbContext)context;
        string adminRoleId = db.Roles.Where(r => r.Name == RoleNames.Admin).Select(r => r.Id).Single();

        if (!db.UserRoles.Any(ur => ur.RoleId == adminRoleId))
        {
            AddInitialAdmin(db, adminRoleId, configuration);
        }

        if (!db.Categories.Any())
        {
            AddCatalog(db);
        }

        if (!db.Extras.Any())
        {
            AddExtras(db);
        }

        db.SaveChanges();

        if (!configuration.GetValue<bool>(SeedDemoSalesKey) || db.Sales.Any())
        {
            return;
        }

        List<Product> products = ActiveProducts(db).ToList();
        List<Extra> extras = db.Extras.AsNoTracking().Where(e => e.IsActive).ToList();
        if (products.Count == 0)
        {
            return;
        }

        string cashierRoleId = db.Roles.Where(r => r.Name == RoleNames.Cashier).Select(r => r.Id).Single();
        string adminId = AdminIds(db, adminRoleId).First();
        List<ApplicationUser> existingCashiers = DemoCashiers(db).ToList();

        AddDemoSales(db, products, extras, existingCashiers, cashierRoleId, adminId);
        db.SaveChanges();
    }

    public static async Task SeedAsync(DbContext context, IConfiguration configuration, CancellationToken cancellationToken)
    {
        ApplicationDbContext db = (ApplicationDbContext)context;
        string adminRoleId = await db.Roles
            .Where(r => r.Name == RoleNames.Admin)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);

        if (!await db.UserRoles.AnyAsync(ur => ur.RoleId == adminRoleId, cancellationToken))
        {
            AddInitialAdmin(db, adminRoleId, configuration);
        }

        if (!await db.Categories.AnyAsync(cancellationToken))
        {
            AddCatalog(db);
        }

        if (!await db.Extras.AnyAsync(cancellationToken))
        {
            AddExtras(db);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (!configuration.GetValue<bool>(SeedDemoSalesKey) || await db.Sales.AnyAsync(cancellationToken))
        {
            return;
        }

        List<Product> products = await ActiveProducts(db).ToListAsync(cancellationToken);
        List<Extra> extras = await db.Extras.AsNoTracking().Where(e => e.IsActive).ToListAsync(cancellationToken);
        if (products.Count == 0)
        {
            return;
        }

        string cashierRoleId = await db.Roles
            .Where(r => r.Name == RoleNames.Cashier)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);
        string adminId = await AdminIds(db, adminRoleId).FirstAsync(cancellationToken);
        List<ApplicationUser> existingCashiers = await DemoCashiers(db).ToListAsync(cancellationToken);

        AddDemoSales(db, products, extras, existingCashiers, cashierRoleId, adminId);
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

    private static void AddCatalog(ApplicationDbContext db)
    {
        foreach (CategorySeed categorySeed in CatalogSeedData.Categories)
        {
            Category category = new() { Name = categorySeed.Name };

            foreach (SubcategorySeed subcategorySeed in categorySeed.Subcategories)
            {
                Subcategory subcategory = new() { Name = subcategorySeed.Name, Category = category };
                category.Subcategories.Add(subcategory);

                db.Products.AddRange(subcategorySeed.Products.Select(p => new Product
                {
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Subcategory = subcategory,
                }));
            }

            db.Categories.Add(category);
        }
    }

    private static void AddExtras(ApplicationDbContext db) =>
        db.Extras.AddRange(CatalogSeedData.Extras.Select(e => new Extra { Name = e.Name, Price = e.Price }));

    private static IQueryable<Product> ActiveProducts(ApplicationDbContext db) =>
        db.Products
            .AsNoTracking()
            .Include(p => p.Subcategory)
            .ThenInclude(s => s!.Category)
            .Where(p => p.IsActive && p.Subcategory!.IsActive && p.Subcategory.Category!.IsActive)
            .OrderBy(p => p.Name);

    private static IQueryable<string> AdminIds(ApplicationDbContext db, string adminRoleId) =>
        db.UserRoles.Where(ur => ur.RoleId == adminRoleId).OrderBy(ur => ur.UserId).Select(ur => ur.UserId);

    private static IQueryable<ApplicationUser> DemoCashiers(ApplicationDbContext db)
    {
        string[] normalizedUserNames = DemoSalesGenerator.Cashiers.Select(c => c.UserName.ToUpperInvariant()).ToArray();

        return db.Users.AsNoTracking().Where(u => normalizedUserNames.Contains(u.NormalizedUserName!));
    }

    private static void AddDemoSales(
        ApplicationDbContext db,
        List<Product> products,
        List<Extra> extras,
        List<ApplicationUser> existingCashiers,
        string cashierRoleId,
        string adminId)
    {
        List<string> cashierIds = existingCashiers.Select(u => u.Id).ToList();

        foreach ((string userName, string fullName) in DemoSalesGenerator.Cashiers)
        {
            if (existingCashiers.Any(u => u.NormalizedUserName == userName.ToUpperInvariant()))
            {
                continue;
            }

            ApplicationUser cashier = new()
            {
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                FullName = fullName,
                LockoutEnabled = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            };

            db.Users.Add(cashier);
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = cashier.Id, RoleId = cashierRoleId });
            cashierIds.Add(cashier.Id);
        }

        db.Sales.AddRange(DemoSalesGenerator.Generate(products, extras, cashierIds, adminId, DateTimeOffset.UtcNow));
    }
}
