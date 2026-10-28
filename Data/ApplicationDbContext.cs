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
    }
}
