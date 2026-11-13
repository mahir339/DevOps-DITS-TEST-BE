using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<User>(u =>
    {
        u.ToTable("users");
        u.HasKey(x => x.Id);
        u.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        u.Property(x => x.Email).HasColumnName("email").HasMaxLength(254);
        u.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(100);
        u.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(200);
        u.Property(x => x.Roles).HasColumnName("roles"); // PostgreSQL text[]
        u.Property(x => x.CreatedAt).HasColumnName("created_at");
        u.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
        u.Property(x => x.FailedLoginCount).HasColumnName("failed_login_count");
        u.Property(x => x.LockedUntil).HasColumnName("locked_until");
        u.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_users_email");
    });
}
