using Customers.Application.Abstractions;
using Customers.Infrastructure.Persistence;
using DevOpsStore.BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Customers.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "CustomersDb";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // PostgreSQL: ConnectionStrings:CustomersDb (DevOpsStore.BuildingBlocks.Persistence package).
        builder.AddPostgresDbContext<CustomersDbContext>(DatabaseConnectionName);
        builder.Services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        return builder;
    }
}
