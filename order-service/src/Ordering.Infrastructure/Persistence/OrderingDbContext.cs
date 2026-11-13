using DevOpsStore.BuildingBlocks.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Entities;

namespace Ordering.Infrastructure.Persistence;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("orders");
            order.HasKey(o => o.Id);
            order.Property(o => o.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            order.Property(o => o.CustomerId).HasColumnName("customer_id");
            order.Property(o => o.CustomerName).HasColumnName("customer_name").HasMaxLength(200);
            order.Property(o => o.CustomerEmail).HasColumnName("customer_email").HasMaxLength(254);
            order.Property(o => o.Status).HasColumnName("status").HasMaxLength(20);
            order.Property(o => o.CreatedAt).HasColumnName("created_at");
            order.Property(o => o.CancelledAt).HasColumnName("cancelled_at");
            order.Ignore(o => o.Total);
            order.HasIndex(o => o.CustomerId).HasDatabaseName("ix_orders_customer_id");

            order.OwnsMany(o => o.Lines, line =>
            {
                line.ToTable("order_lines");
                line.WithOwner().HasForeignKey("order_id");
                line.Property<int>("id").UseIdentityByDefaultColumn();
                line.HasKey("id");
                line.Property(l => l.ProductId).HasColumnName("product_id");
                line.Property(l => l.ProductName).HasColumnName("product_name").HasMaxLength(100);
                line.Property(l => l.Quantity).HasColumnName("quantity");
                line.Property(l => l.UnitPrice).HasColumnName("unit_price").HasPrecision(10, 2);
                line.Ignore(l => l.LineTotal);
            });
            order.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // Events waiting to be published (DevOpsStore.BuildingBlocks.Messaging package).
        modelBuilder.AddOutbox();
    }
}
