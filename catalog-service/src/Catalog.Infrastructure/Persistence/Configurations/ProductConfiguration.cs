using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        // Seed rows use ids 1-6, so generated ids start at 100 to avoid colliding with them.
        builder.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 100);
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Category).HasColumnName("category").HasMaxLength(50).IsRequired();
        builder.Property(p => p.Price).HasColumnName("price").HasPrecision(10, 2);
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(500);

        builder.HasIndex(p => p.Category).HasDatabaseName("ix_products_category");

        builder.HasData(SeedData.Products());
    }
}
