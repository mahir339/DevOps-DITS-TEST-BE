using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Notification>(n =>
    {
        n.ToTable("notifications");
        n.HasKey(x => x.Id);
        n.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        n.Property(x => x.Recipient).HasColumnName("recipient").HasMaxLength(254);
        n.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(200);
        n.Property(x => x.Message).HasColumnName("message").HasMaxLength(4000);
        n.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(20);
        n.Property(x => x.Type).HasColumnName("type").HasMaxLength(30);
        n.Property(x => x.OrderId).HasColumnName("order_id");
        n.Property(x => x.SourceMessageId).HasColumnName("source_message_id");
        n.Property(x => x.CreatedAt).HasColumnName("created_at");

        // One notification per broker message: a redelivered event cannot create a duplicate.
        n.HasIndex(x => x.SourceMessageId).IsUnique().HasFilter("source_message_id IS NOT NULL")
            .HasDatabaseName("ux_notifications_source_message_id");
        n.HasIndex(x => x.OrderId).HasDatabaseName("ix_notifications_order_id");
    });
}
