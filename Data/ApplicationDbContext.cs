using DulceAtardecer.Constants;
using DulceAtardecer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DulceAtardecer.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    private const int UserIdLength = 450;
    private const int IpAddressLength = 45;
    private const string CaseInsensitiveText = "citext";
    private const string NameLengthCheck = "char_length(\"Name\") BETWEEN 2 AND 50";

    private static readonly IdentityRole[] SeedRoles =
    [
        new() { Id = "0199f0a0-0000-7000-8000-000000000001", Name = RoleNames.Admin, NormalizedName = "ADMIN", ConcurrencyStamp = "0199f0a0-0000-7000-8000-000000000001" },
        new() { Id = "0199f0a0-0000-7000-8000-000000000002", Name = RoleNames.Cashier, NormalizedName = "CASHIER", ConcurrencyStamp = "0199f0a0-0000-7000-8000-000000000002" },
        new() { Id = "0199f0a0-0000-7000-8000-000000000003", Name = RoleNames.Kitchen, NormalizedName = "KITCHEN", ConcurrencyStamp = "0199f0a0-0000-7000-8000-000000000003" },
    ];

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<Extra> Extras => Set<Extra>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SaleItemExtra> SaleItemExtras => Set<SaleItemExtra>();
    public DbSet<SaleModification> SaleModifications => Set<SaleModification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("citext");

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.FullName).HasMaxLength(100);
            user.Property(u => u.DeactivatedById).HasMaxLength(UserIdLength);
            user.Property(u => u.CreatedById).HasMaxLength(UserIdLength);
            user.Property(u => u.UpdatedById).HasMaxLength(UserIdLength);
        });

        builder.Entity<IdentityRole>().HasData(SeedRoles);

        builder.Entity<Session>(session =>
        {
            session.Property(s => s.RevocationReason).HasMaxLength(100);
            session.Property(s => s.UserAgent).HasMaxLength(512);
            session.Property(s => s.IpAddress).HasMaxLength(IpAddressLength);
            session.HasIndex(s => s.UserId);
            session.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            session.HasMany(s => s.RefreshTokens)
                .WithOne(t => t.Session)
                .HasForeignKey(t => t.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(token =>
        {
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.HasIndex(t => t.TokenHash).IsUnique();
        });

        builder.Entity<AuditEvent>(auditEvent =>
        {
            auditEvent.Property(e => e.UserId).HasMaxLength(UserIdLength);
            auditEvent.Property(e => e.Action).HasMaxLength(100);
            auditEvent.Property(e => e.EntityType).HasMaxLength(100);
            auditEvent.Property(e => e.EntityId).HasMaxLength(64);
            auditEvent.Property(e => e.IpAddress).HasMaxLength(IpAddressLength);
            auditEvent.HasIndex(e => e.OccurredAt);
            auditEvent.HasIndex(e => e.UserId);
        });

        builder.Entity<Category>(category =>
        {
            category.ToTable(t => t.HasCheckConstraint("CK_Categories_Name_Length", NameLengthCheck));
            category.Property(c => c.Name).HasColumnType(CaseInsensitiveText);
            category.Property(c => c.CreatedById).HasMaxLength(UserIdLength);
            category.Property(c => c.UpdatedById).HasMaxLength(UserIdLength);
            category.HasIndex(c => c.Name).IsUnique();
            category.HasMany(c => c.Subcategories)
                .WithOne(s => s.Category)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Subcategory>(subcategory =>
        {
            subcategory.ToTable(t => t.HasCheckConstraint("CK_Subcategories_Name_Length", NameLengthCheck));
            subcategory.Property(s => s.Name).HasColumnType(CaseInsensitiveText);
            subcategory.Property(s => s.CreatedById).HasMaxLength(UserIdLength);
            subcategory.Property(s => s.UpdatedById).HasMaxLength(UserIdLength);
            subcategory.HasIndex(s => new { s.CategoryId, s.Name }).IsUnique();
        });

        builder.Entity<Extra>(extra =>
        {
            extra.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Extras_Name_Length", NameLengthCheck);
                t.HasCheckConstraint("CK_Extras_Price_Range", "\"Price\" BETWEEN 0 AND 9999.99");
            });
            extra.Property(e => e.Name).HasColumnType(CaseInsensitiveText);
            extra.Property(e => e.Price).HasPrecision(18, 2);
            extra.Property(e => e.CreatedById).HasMaxLength(UserIdLength);
            extra.Property(e => e.UpdatedById).HasMaxLength(UserIdLength);
            extra.HasIndex(e => e.Name).IsUnique();
        });

        builder.Entity<Product>(product =>
        {
            product.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Products_Name_Length", "char_length(\"Name\") BETWEEN 2 AND 100");
                t.HasCheckConstraint("CK_Products_Price_Range", "\"Price\" > 0 AND \"Price\" <= 9999.99");
            });
            product.Property(p => p.Name).HasColumnType(CaseInsensitiveText);
            product.Property(p => p.Description).HasMaxLength(250);
            product.Property(p => p.Price).HasPrecision(18, 2);
            product.Property(p => p.CreatedById).HasMaxLength(UserIdLength);
            product.Property(p => p.UpdatedById).HasMaxLength(UserIdLength);
            product.HasIndex(p => p.Name).IsUnique();
            product.HasOne(p => p.Subcategory)
                .WithMany()
                .HasForeignKey(p => p.SubcategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Sale>(sale =>
        {
            sale.Property(s => s.CashierId).HasMaxLength(UserIdLength);
            sale.Property(s => s.IdempotencyKey).HasMaxLength(100);
            sale.Property(s => s.CustomerDescription).HasMaxLength(100);
            sale.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            sale.Property(s => s.Subtotal).HasPrecision(18, 2);
            sale.Property(s => s.Tax).HasPrecision(18, 2);
            sale.Property(s => s.Total).HasPrecision(18, 2);
            sale.Property(s => s.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            sale.Property(s => s.PaidById).HasMaxLength(UserIdLength);
            sale.Property(s => s.CancelReason).HasMaxLength(200);
            sale.Property(s => s.CancelledById).HasMaxLength(UserIdLength);
            sale.Property(s => s.CreatedById).HasMaxLength(UserIdLength);
            sale.Property(s => s.UpdatedById).HasMaxLength(UserIdLength);
            sale.Property(s => s.Version).IsRowVersion();
            sale.HasIndex(s => new { s.CashierId, s.IdempotencyKey }).IsUnique();
            sale.HasIndex(s => new { s.Status, s.CreatedAt });
            sale.HasOne(s => s.Cashier)
                .WithMany()
                .HasForeignKey(s => s.CashierId)
                .OnDelete(DeleteBehavior.Restrict);
            sale.HasMany(s => s.Items)
                .WithOne()
                .HasForeignKey(i => i.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SaleItem>(item =>
        {
            item.ToTable(t => t.HasCheckConstraint("CK_SaleItems_Quantity_Range", "\"Quantity\" BETWEEN 1 AND 50"));
            item.Property(i => i.ProductName).HasMaxLength(100);
            item.Property(i => i.CategoryName).HasMaxLength(50);
            item.Property(i => i.SubcategoryName).HasMaxLength(50);
            item.Property(i => i.ProductPrice).HasPrecision(18, 2);
            item.Property(i => i.UnitPrice).HasPrecision(18, 2);
            item.Property(i => i.Amount).HasPrecision(18, 2);
            item.Property(i => i.Note).HasMaxLength(200);
            item.HasOne<Product>()
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            item.HasMany(i => i.Extras)
                .WithOne()
                .HasForeignKey(e => e.SaleItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SaleItemExtra>(extra =>
        {
            extra.ToTable(t => t.HasCheckConstraint("CK_SaleItemExtras_Quantity_Range", "\"Quantity\" BETWEEN 1 AND 10"));
            extra.Property(e => e.Name).HasMaxLength(50);
            extra.Property(e => e.Price).HasPrecision(18, 2);
            extra.HasIndex(e => new { e.SaleItemId, e.ExtraId }).IsUnique();
            extra.HasOne<Extra>()
                .WithMany()
                .HasForeignKey(e => e.ExtraId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SaleModification>(modification =>
        {
            modification.Property(m => m.ModifiedById).HasMaxLength(UserIdLength);
            modification.Property(m => m.PreviousTotal).HasPrecision(18, 2);
            modification.Property(m => m.NewTotal).HasPrecision(18, 2);
            modification.HasIndex(m => m.SaleId);
            modification.HasOne<Sale>()
                .WithMany()
                .HasForeignKey(m => m.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
