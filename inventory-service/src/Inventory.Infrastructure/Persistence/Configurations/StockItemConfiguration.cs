using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    private static readonly DateTimeOffset Seeded = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items", table =>
            table.HasCheckConstraint("ck_stock_items_quantity_not_negative", "quantity >= 0"));
        builder.HasKey(s => s.ProductId);
        builder.Property(s => s.ProductId).HasColumnName("product_id").ValueGeneratedNever();
        builder.Property(s => s.Quantity).HasColumnName("quantity");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        // Starting stock for the six products seeded by catalog-service.
        int[] quantities = [50, 20, 8, 35, 12, 3];
        builder.HasData(quantities.Select((quantity, index) => new StockItem(index + 1, quantity, Seeded)));
    }
}

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("processed_messages");
        builder.HasKey(m => m.MessageId);
        builder.Property(m => m.MessageId).HasColumnName("message_id").ValueGeneratedNever();
        builder.Property(m => m.RoutingKey).HasColumnName("routing_key").HasMaxLength(200);
        builder.Property(m => m.ProcessedAt).HasColumnName("processed_at");
    }
}
