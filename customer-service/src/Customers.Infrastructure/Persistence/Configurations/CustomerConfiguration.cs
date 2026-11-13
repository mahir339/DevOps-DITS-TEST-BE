using Customers.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Customers.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);

        // Seed rows use ids 1-4, so generated ids start at 100.
        builder.Property(c => c.Id).HasColumnName("id").UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 100);
        builder.Property(c => c.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(c => c.City).HasColumnName("city").HasMaxLength(100);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");

        // Emails are stored lower-case by the entity, so a plain unique index is enough.
        builder.HasIndex(c => c.Email).IsUnique().HasDatabaseName("ux_customers_email");

        builder.HasData(SeedData.Customers());
    }
}
